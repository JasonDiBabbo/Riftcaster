using System.Reflection;
using Microsoft.Extensions.Logging;
using Riftcaster.Contracts;
using Riftcaster.Core.Updates;
using Velopack;
using Velopack.Sources;

namespace Riftcaster.Windows;

/// <summary>
/// Keeps an installed copy up to date from GitHub Releases (#62), as the Settings page's updates section
/// says (see <see cref="UpdateService"/>). It checks shortly after starting and every few hours, and
/// downloads a newer release quietly. A downloaded update waits: it's installed when Riftcaster quits
/// (in Automatic), or straight away if Restart to update is chosen, never in the middle of a show.
/// </summary>
/// <remarks>
/// Does nothing in a copy that wasn't installed by Setup.exe or unzipped from the portable zip,
/// such as development's dotnet run, since there's nothing to update.
/// </remarks>
internal sealed class Updates : IUpdateFetcher, IDisposable
{
    /// <summary>
    /// The setting for where releases come from: the GitHub repository, another address, or a
    /// folder, which is useful for trying an update without publishing it. Empty turns updates off.
    /// </summary>
    public const string Setting = "Updates:Source";

    /// <summary>
    /// Where releases come from unless the setting says otherwise.
    /// </summary>
    public const string DefaultSource = "https://github.com/JasonDiBabbo/Riftcaster";

    // Long enough after starting that the check doesn't compete with the server's own startup.
    private static readonly TimeSpan FirstCheck = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(4);

    private readonly string _source;

    private readonly UpdateService _service;

    private readonly ILogger _logger;

    private readonly bool _installed;

    private readonly Dictionary<UpdateChannel, UpdateManager> _managers = [];

    private readonly System.Windows.Forms.Timer _timer = new();

    private VelopackAsset? _ready;

    /// <summary>
    /// Sets up updates. Call <see cref="Start"/> once the tray is showing.
    /// </summary>
    /// <param name="source">The <see cref="Setting"/>, or null for <see cref="DefaultSource"/>.</param>
    /// <param name="service">What the Settings page and the tray know about updating.</param>
    /// <param name="logger">Where to log what was found, downloaded or failed.</param>
    public Updates(string? source, UpdateService service, ILogger logger)
    {
        _source = source ?? DefaultSource;
        _service = service;
        _logger = logger;

        // Whether this copy is installed doesn't depend on where releases come from, which may be empty.
        var manager = new UpdateManager(DefaultSource);
        _installed = manager.IsInstalled;
        IsPortable = manager.IsPortable;
        if (_installed && string.IsNullOrWhiteSpace(_source))
        {
            _logger.LogInformation("Updates are turned off ({Setting} is empty).", Setting);
            _service.Unavailable(UpdateAvailability.TurnedOffInConfiguration);
        }
    }

    /// <summary>
    /// The version running, such as 0.2.0 or 0.3.0-beta.1, as the server's /api/info reports it.
    /// </summary>
    public static string RunningVersion { get; } =
        (typeof(Updates).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0").Split('+')[0];

    /// <summary>
    /// Whether this copy was installed by Setup.exe or unzipped from the portable zip, rather than run from a build folder.
    /// </summary>
    public bool IsInstalled => _installed;

    /// <summary>
    /// Whether this copy was unzipped from the portable zip, which can be moved, rather than installed by Setup.exe.
    /// </summary>
    public bool IsPortable { get; }

    /// <inheritdoc />
    public string? DownloadedVersion => _ready?.Version.ToString();

    /// <summary>
    /// Starts checking: soon, then every few hours, as the updates settings allow.
    /// </summary>
    /// <param name="restartToUpdate">Quits Riftcaster so that the downloaded update installs, and starts it again.</param>
    public void Start(Action restartToUpdate)
    {
        if (!_installed || string.IsNullOrWhiteSpace(_source))
        {
            return;
        }

        // Downloaded last time but never installed, if Riftcaster stopped without quitting.
        _ready = Manager(UpdateChannel.Stable).UpdatePendingRestart;
        _service.Attach(this, restartToUpdate);

        _timer.Tick += async (_, _) =>
        {
            _timer.Interval = (int)CheckInterval.TotalMilliseconds;
            await _service.CheckAsync();
        };
        _timer.Interval = (int)FirstCheck.TotalMilliseconds;
        _timer.Start();
    }

    /// <inheritdoc />
    public async Task<string?> CheckAndDownloadAsync(UpdateChannel channel, CancellationToken cancellationToken)
    {
        var manager = Manager(channel);
        var update = await manager.CheckForUpdatesAsync();
        if (update is null)
        {
            return null;
        }

        _logger.LogInformation("Downloading Riftcaster {Version}.", update.TargetFullRelease.Version);
        await manager.DownloadUpdatesAsync(update, cancelToken: cancellationToken);
        _ready = update.TargetFullRelease;
        return _ready.Version.ToString();
    }

    /// <summary>
    /// Installs the downloaded update, if there is one and it's to be installed, once Riftcaster has
    /// exited. Call it last, after the server has stopped: the updater waits for this process to end.
    /// </summary>
    /// <param name="restart">Start Riftcaster again afterwards, showing the update's progress. It was asked for, so install.</param>
    /// <param name="restartArgs">The arguments to start it again with.</param>
    public void InstallOnExit(bool restart, string[] restartArgs)
    {
        // Quitting installs it only in Automatic; Ask first waits for Restart to update, and Off never installs.
        if (_ready is null || !(restart || _service.InstallsOnQuit))
        {
            return;
        }

        _logger.LogInformation("Installing Riftcaster {Version} as it exits.", _ready.Version);
        Manager(UpdateChannel.Stable).WaitExitThenApplyUpdates(_ready, silent: !restart, restart, restartArgs);
    }

    public void Dispose() => _timer.Dispose();

    // One per channel: the channel is part of how the release list is read, and can change while running.
    private UpdateManager Manager(UpdateChannel channel)
    {
        if (!_managers.TryGetValue(channel, out var manager))
        {
            manager = _source.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase)
                ? new UpdateManager(new GithubSource(_source, accessToken: null, prerelease: channel == UpdateChannel.PreRelease))
                : new UpdateManager(_source);
            _managers[channel] = manager;
        }

        return manager;
    }
}
