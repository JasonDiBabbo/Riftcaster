using System.Diagnostics;
using Riftcaster.Contracts;
using Riftcaster.Core.Updates;

namespace Riftcaster.Windows;

/// <summary>
/// Riftcaster's icon by the clock, in place of a console window (#68). Its menu names the version
/// running, then has Open Riftcaster, Open log folder and Quit; double-clicking the icon also opens
/// the web interface. Once an update is downloaded (#62), it says so and the menu adds Restart to update,
/// unless updates are turned off on the Settings page.
/// </summary>
internal sealed class TrayContext : ApplicationContext
{
    private readonly Func<Uri> _webUI;

    private readonly NotifyIcon _icon;

    private readonly ToolStripMenuItem _restartToUpdate;

    private readonly UpdateService _updates;

    // The tray's own thread: the web interface asks for restarts, and tells of changes, from others.
    private readonly SynchronizationContext _ui;

    // The version the balloon last told of, so that it's told once.
    private string? _announced;

    // The logo at the tray's size for this screen's scaling: 16 px at 100%, 24 at 150%, and so on.
    private readonly Icon _logo = LoadLogo(SystemInformation.SmallIconSize);

    /// <summary>
    /// Shows the icon.
    /// </summary>
    /// <param name="webUI">
    /// The web interface's address, on this computer. Asked each time, not once: the port can be switched
    /// while Riftcaster runs.
    /// </param>
    /// <param name="logFolder">The folder where log files are stored.</param>
    /// <param name="updates">Updates, for the menu to offer one once it's downloaded.</param>
    public TrayContext(Func<Uri> webUI, string logFolder, UpdateService updates)
    {
        _webUI = webUI;
        _updates = updates;

        // Hidden until an update is ready.
        _restartToUpdate = new ToolStripMenuItem("Restart to update", null, (_, _) => _updates.RestartToUpdate()) { Visible = false };

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem($"Riftcaster {Updates.RunningVersion}") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Open Riftcaster", null, (_, _) => OpenWebUI(_webUI()));
        menu.Items.Add("Open log folder", null, (_, _) => Open(logFolder));
        menu.Items.Add(_restartToUpdate);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => ExitThread());

        _icon = new NotifyIcon
        {
            Icon = _logo,
            Text = "Riftcaster",
            Visible = true,
            ContextMenuStrip = menu,
        };

        _icon.DoubleClick += (_, _) => OpenWebUI(_webUI());

        // Creating the menu installed the tray's synchronization context.
        _ui = SynchronizationContext.Current ?? new SynchronizationContext();
        _updates.Changed += OnUpdatesChanged;
        ShowUpdates();
    }

    /// <summary>
    /// Whether Restart to update was chosen, rather than Quit.
    /// </summary>
    public bool RestartRequested { get; private set; }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _updates.Changed -= OnUpdatesChanged;

            // Otherwise the icon can stay by the clock after quitting, until the mouse passes over it.
            _icon.ContextMenuStrip?.Dispose();
            _icon.Dispose();
            _logo.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Opens the web interface in the default browser.
    /// </summary>
    /// <param name="webUI">The web interface's address.</param>
    public static void OpenWebUI(Uri webUI) => Open(webUI.ToString());

    /// <summary>
    /// Quits so that the downloaded update installs, and Riftcaster starts again. Called from any thread.
    /// </summary>
    public void RestartToUpdate() => _ui.Post(_ =>
    {
        RestartRequested = true;
        ExitThread();
    }, null);

    private void OnUpdatesChanged() => _ui.Post(_ => ShowUpdates(), null);

    // Restart to update is there once an update is downloaded, unless updates are off: then it wouldn't be
    // installed, and the Settings page says so. Restarting stops the server, and the overlays with it, for
    // a few seconds, so it's only ever asked for; otherwise the update waits until Riftcaster quits.
    private void ShowUpdates()
    {
        var version = _updates.Status.ReadyVersion;
        _restartToUpdate.Visible = _updates.CanRestartToUpdate;
        if (version is null || !_updates.CanRestartToUpdate)
        {
            return;
        }

        _restartToUpdate.Text = $"Restart to update to {version}";
        if (_announced == version)
        {
            return;
        }

        _announced = version;
        var install = _updates.Mode == UpdateMode.Automatic
            ? "It will install the next time Riftcaster closes. To install it now, right-click"
            : "To install it, right-click";
        _icon.ShowBalloonTip(
            10_000,
            $"Riftcaster {version} is ready to install",
            $"{install} the Riftcaster icon and choose \"Restart to update to {version}\".",
            ToolTipIcon.Info);
    }

    private static Icon LoadLogo(Size size)
    {
        using var stream = typeof(TrayContext).Assembly.GetManifestResourceStream("Riftcaster.ico")!;
        return new Icon(stream, size);
    }

    // UseShellExecute: Windows opens it as the Run box would, an address in the default browser
    // and a folder in File Explorer.
    //
    // Not from Riftcaster's own folder, which is where it would start: a browser opened this way keeps running
    // with that folder as its working directory, and an update can't replace a folder something is working in.
    private static void Open(string target) => Process.Start(new ProcessStartInfo(target)
    {
        UseShellExecute = true,
        WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    });
}
