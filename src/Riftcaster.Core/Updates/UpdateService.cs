using Microsoft.Extensions.Logging;
using Riftcaster.Contracts;
using Riftcaster.Core.Settings;

namespace Riftcaster.Core.Updates;

/// <summary>
/// What the Settings page and the tray know about updating: the settings that decide it, the state it's
/// in, and the two things that can be asked of it, to check now and to restart into a downloaded update.
/// </summary>
/// <remarks>
/// <para>
/// Fetching and installing belong to the Windows app, which attaches its updater (<see cref="Attach"/>)
/// once it has one. Without that (the console server, or running from source), there is nothing to update,
/// and <see cref="Availability"/> says why.
/// </para>
/// <para>
/// A downloaded update stays downloaded when updates are turned off, but isn't installed: neither on
/// quitting nor by restarting.
/// </para>
/// <para>
/// A downloaded update belongs to the channel it came from. Changing the channel deletes it, so that quitting
/// or restarting never installs a release from the channel that was just left.
/// </para>
/// </remarks>
public sealed class UpdateService
{
    private readonly AppSettingsService _settings;

    private readonly TimeProvider _time;

    private readonly ILogger<UpdateService> _logger;

    private readonly Lock _lock = new();

    private IUpdateFetcher? _source;

    private Action? _restartToUpdate;

    private volatile UpdateAvailability _availability;

    private volatile UpdateStatus _status = new(UpdatePhase.NotChecked, null, null);

    private UpdateMode _mode;

    private UpdateChannel _channel;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="settings">Where the mode and channel are chosen.</param>
    /// <param name="runningVersion">The version running, such as 0.2.0 or 0.3.0-beta.1.</param>
    /// <param name="availability">Why there's nothing to update, until an updater is attached.</param>
    /// <param name="time">The clock, which tests control.</param>
    /// <param name="logger">Logs what was found, downloaded or failed.</param>
    public UpdateService(
        AppSettingsService settings,
        string runningVersion,
        UpdateAvailability availability,
        TimeProvider time,
        ILogger<UpdateService> logger)
    {
        _settings = settings;
        RunningVersion = runningVersion;
        _availability = availability;
        _time = time;
        _logger = logger;
        _mode = settings.Settings.Updates.Mode;
        _channel = Channel;
        settings.Changed += OnSettingsChanged;
    }

    /// <summary>
    /// The version running, such as 0.2.0 or 0.3.0-beta.1.
    /// </summary>
    public string RunningVersion { get; }

    /// <summary>
    /// Whether this copy can update, and if not, why.
    /// </summary>
    public UpdateAvailability Availability => _availability;

    /// <summary>
    /// How far updating has got.
    /// </summary>
    public UpdateStatus Status => _status;

    /// <summary>
    /// What Riftcaster does about a newer version.
    /// </summary>
    public UpdateMode Mode => _settings.Settings.Updates.Mode;

    /// <summary>
    /// Which releases it looks for: the one chosen, or else the kind this build is (a pre-release build
    /// keeps to pre-releases).
    /// </summary>
    public UpdateChannel Channel =>
        _settings.Settings.Updates.Channel ?? (RunningVersion.Contains('-') ? UpdateChannel.PreRelease : UpdateChannel.Stable);

    /// <summary>
    /// Whether a check can be asked for now: this copy updates, updates are on, nothing is downloaded
    /// waiting to install, and a check isn't already running.
    /// </summary>
    public bool CanCheck =>
        Availability == UpdateAvailability.Updatable && Mode != UpdateMode.Off && Status.Phase is not (UpdatePhase.Checking or UpdatePhase.Ready);

    /// <summary>
    /// Whether Restart to update does anything: an update is downloaded, and updates aren't off.
    /// </summary>
    public bool CanRestartToUpdate => Status.ReadyVersion is not null && Mode != UpdateMode.Off;

    /// <summary>
    /// Whether the downloaded update installs by itself when Riftcaster quits: only in Automatic.
    /// </summary>
    public bool InstallsOnQuit => Status.ReadyVersion is not null && Mode == UpdateMode.Automatic;

    /// <summary>
    /// Raised when the status, the mode or the channel changes.
    /// </summary>
    /// <remarks>
    /// May be raised on any thread.
    /// </remarks>
    public event Action? Changed;

    /// <summary>
    /// Gives the service an updater to check and install with, which makes this copy updatable.
    /// </summary>
    /// <param name="source">Fetches the updates.</param>
    /// <param name="restartToUpdate">Quits Riftcaster so that the downloaded update installs, and starts it again.</param>
    public void Attach(IUpdateFetcher source, Action restartToUpdate)
    {
        lock (_lock)
        {
            _source = source;
            _restartToUpdate = restartToUpdate;
            _availability = UpdateAvailability.Updatable;

            // Downloaded last time but never installed, if Riftcaster stopped without quitting.
            if (source.DownloadedVersion is { } waiting)
            {
                _status = new UpdateStatus(UpdatePhase.Ready, null, waiting);
            }
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Says why this copy doesn't update, when the updater has found out it can't.
    /// </summary>
    /// <param name="availability">Why.</param>
    public void Unavailable(UpdateAvailability availability)
    {
        _availability = availability;
        Changed?.Invoke();
    }

    /// <summary>
    /// Looks for a newer version, and downloads it. Does nothing if updates are off, one is downloaded
    /// already, or a check is running.
    /// </summary>
    /// <param name="cancellationToken">Stops the check, such as when the server is stopping.</param>
    public async Task CheckAsync(CancellationToken cancellationToken = default)
    {
        IUpdateFetcher source;
        UpdateStatus before;
        lock (_lock)
        {
            if (_source is not { } attached || Mode == UpdateMode.Off || _status.Phase is UpdatePhase.Checking or UpdatePhase.Ready)
            {
                return;
            }

            source = attached;
            before = _status;
            _status = _status with { Phase = UpdatePhase.Checking };
        }

        Changed?.Invoke();

        try
        {
            var channel = Channel;
            _logger.LogInformation("Looking for a newer version than {Version} ({Channel}).", RunningVersion, channel);
            var version = await source.CheckAndDownloadAsync(channel, cancellationToken);

            // The channel was changed while it downloaded: what came is from the channel that was left.
            var stale = version is not null && channel != Channel;
            if (stale)
            {
                Discard(source, version!);
            }

            lock (_lock)
            {
                _status = stale ? new UpdateStatus(UpdatePhase.NotChecked, null, null)
                    : version is null ? new UpdateStatus(UpdatePhase.UpToDate, _time.GetUtcNow(), null)
                    : new UpdateStatus(UpdatePhase.Ready, _time.GetUtcNow(), version);
            }

            if (stale)
            {
                // Nothing to say: it was discarded.
            }
            else if (version is not null)
            {
                _logger.LogInformation("Riftcaster {Version} is ready to install.", version);
            }
            else
            {
                _logger.LogInformation("Riftcaster {Version} is the newest version.", RunningVersion);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Stopped, not failed: it looks as it did before, and whoever stopped it is told.
            lock (_lock)
            {
                _status = before;
            }

            Changed?.Invoke();
            throw;
        }
        catch (UpdateSourceMissingException exception)
        {
            // The setting's doing: said plainly, with no stack, and in Settings. The next check looks again.
            _logger.LogWarning("Couldn't check for an update: {Problem} Fix or remove Updates:Source.", exception.Message);
            lock (_lock)
            {
                _status = new UpdateStatus(UpdatePhase.Failed, _time.GetUtcNow(), null, exception.Message);
            }
        }
        catch (Exception exception)
        {
            // Often just no internet, as at a venue; the next check tries again.
            _logger.LogWarning(exception, "Couldn't check for or download an update.");
            lock (_lock)
            {
                _status = new UpdateStatus(UpdatePhase.Failed, _time.GetUtcNow(), null);
            }
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Quits Riftcaster so that the downloaded update installs, then starts it again. Does nothing unless
    /// <see cref="CanRestartToUpdate"/>.
    /// </summary>
    public void RestartToUpdate()
    {
        if (CanRestartToUpdate && _restartToUpdate is { } restart)
        {
            restart();
        }
    }

    // The update that's downloaded is from the channel it was found on: a different channel is a different answer.
    private void DiscardDownloaded()
    {
        IUpdateFetcher source;
        string version;
        lock (_lock)
        {
            if (_source is not { } attached || _status.ReadyVersion is not { } ready)
            {
                return;
            }

            source = attached;
            version = ready;
            _status = new UpdateStatus(UpdatePhase.NotChecked, null, null);
        }

        Discard(source, version);
    }

    private void Discard(IUpdateFetcher source, string version)
    {
        _logger.LogInformation("Deleting the downloaded Riftcaster {Version}: the update channel changed.", version);
        try
        {
            source.DiscardDownload();
        }
        catch (Exception exception)
        {
            // It won't be installed by this run either way; if the files stay, they are found again at the next start.
            _logger.LogWarning(exception, "Couldn't delete the downloaded Riftcaster {Version}.", version);
        }
    }

    private void OnSettingsChanged()
    {
        var mode = Mode;
        var turnedOn = _mode == UpdateMode.Off && mode != UpdateMode.Off;
        _mode = mode;

        var channel = Channel;
        if (channel != _channel)
        {
            _channel = channel;
            DiscardDownloaded();
        }

        Changed?.Invoke();

        // Turned back on: look now, not at the next scheduled check, which could be hours away.
        if (turnedOn)
        {
            _ = CheckAsync();
        }
    }
}
