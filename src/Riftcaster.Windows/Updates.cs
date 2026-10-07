using System.Reflection;
using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;

namespace Riftcaster.Windows;

/// <summary>
/// Keeps an installed copy up to date from GitHub Releases (#62). It checks shortly after starting
/// and every few hours, and downloads a newer release quietly. A downloaded update waits: it's
/// installed when Riftcaster quits, or straight away if the tray's Restart to update is chosen,
/// never in the middle of a show.
/// </summary>
/// <remarks>
/// Does nothing in a copy that wasn't installed by Setup.exe or unzipped from the portable zip,
/// such as development's dotnet run, since there's nothing to update.
/// </remarks>
internal sealed class Updates : IDisposable
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

    private readonly ILogger _logger;

    private readonly UpdateManager? _manager;

    private readonly System.Windows.Forms.Timer _timer = new();

    private bool _checking;

    /// <summary>
    /// Sets up updates. Call <see cref="Start"/> once the tray is showing.
    /// </summary>
    /// <param name="source">The <see cref="Setting"/>, or null for <see cref="DefaultSource"/>.</param>
    /// <param name="logger">Where to log what was found, downloaded or failed.</param>
    public Updates(string? source, ILogger logger)
    {
        _logger = logger;
        source ??= DefaultSource;

        if (string.IsNullOrWhiteSpace(source))
        {
            _logger.LogInformation("Updates are turned off ({Setting} is empty).", Setting);
            return;
        }

        var manager = source.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase)
            ? new UpdateManager(new GithubSource(source, accessToken: null, prerelease: RunningVersion.Contains('-')))
            : new UpdateManager(source);

        if (!manager.IsInstalled)
        {
            return;
        }

        _manager = manager;

        // Downloaded last time but never installed, if Riftcaster stopped without quitting.
        Ready = manager.UpdatePendingRestart;

        _timer.Tick += async (_, _) =>
        {
            _timer.Interval = (int)CheckInterval.TotalMilliseconds;
            await CheckAsync();
        };
    }

    /// <summary>
    /// The version running, such as 0.2.0 or 0.3.0-beta.1, as the server's /api/info reports it.
    /// </summary>
    public static string RunningVersion { get; } =
        (typeof(Updates).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0").Split('+')[0];

    /// <summary>
    /// The downloaded update, waiting to be installed; null until there is one.
    /// </summary>
    public VelopackAsset? Ready { get; private set; }

    /// <summary>
    /// Raised, on the tray's thread, when an update has been downloaded.
    /// </summary>
    public event EventHandler? ReadyChanged;

    /// <summary>
    /// Starts checking: soon, then every few hours.
    /// </summary>
    public void Start()
    {
        if (_manager is null)
        {
            return;
        }

        if (Ready is not null)
        {
            ReadyChanged?.Invoke(this, EventArgs.Empty);
            return; // Nothing newer is fetched until this one is installed
        }

        _timer.Interval = (int)FirstCheck.TotalMilliseconds;
        _timer.Start();
    }

    /// <summary>
    /// Installs the downloaded update, if there is one, once Riftcaster has exited. Call it last,
    /// after the server has stopped: the updater waits for this process to end.
    /// </summary>
    /// <param name="restart">Start Riftcaster again afterwards, showing the update's progress.</param>
    /// <param name="restartArgs">The arguments to start it again with.</param>
    public void InstallOnExit(bool restart, string[] restartArgs)
    {
        if (_manager is null || Ready is null)
        {
            return;
        }

        _logger.LogInformation("Installing Riftcaster {Version} as it exits.", Ready.Version);
        _manager.WaitExitThenApplyUpdates(Ready, silent: !restart, restart, restartArgs);
    }

    public void Dispose() => _timer.Dispose();

    private async Task CheckAsync()
    {
        if (_manager is null || _checking || Ready is not null)
        {
            return;
        }

        _checking = true;
        try
        {
            var update = await _manager.CheckForUpdatesAsync();
            if (update is null)
            {
                return;
            }

            _logger.LogInformation("Downloading Riftcaster {Version}.", update.TargetFullRelease.Version);
            await _manager.DownloadUpdatesAsync(update);

            Ready = update.TargetFullRelease;
            _timer.Stop();
            _logger.LogInformation("Riftcaster {Version} is ready to install.", Ready.Version);
            ReadyChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            // Often just no internet, as at a venue; the next check tries again.
            _logger.LogWarning(exception, "Couldn't check for or download an update.");
        }
        finally
        {
            _checking = false;
        }
    }
}
