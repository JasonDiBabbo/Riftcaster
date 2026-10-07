using System.Diagnostics;

namespace Riftcaster.Windows;

/// <summary>
/// Riftcaster's icon by the clock, in place of a console window (#68). Its menu names the version
/// running, then has Open dashboard, Open log folder and Quit; double-clicking the icon also opens
/// the dashboard. Once an update is downloaded (#62), it says so and the menu adds Restart to update.
/// </summary>
internal sealed class TrayContext : ApplicationContext
{
    private readonly Uri _dashboard;

    private readonly NotifyIcon _icon;

    private readonly ToolStripMenuItem _restartToUpdate;

    private readonly Updates _updates;

    // The logo at the tray's size for this screen's scaling: 16 px at 100%, 24 at 150%, and so on.
    private readonly Icon _logo = LoadLogo(SystemInformation.SmallIconSize);

    /// <summary>
    /// Shows the icon.
    /// </summary>
    /// <param name="dashboard">The admin dashboard's address, on this computer.</param>
    /// <param name="logFolder">The folder where log files are stored.</param>
    /// <param name="updates">Updates, for the menu to offer one once it's downloaded.</param>
    public TrayContext(Uri dashboard, string logFolder, Updates updates)
    {
        _dashboard = dashboard;
        _updates = updates;

        // Hidden until an update is ready.
        _restartToUpdate = new ToolStripMenuItem("Restart to update", null, (_, _) => RestartToUpdate()) { Visible = false };

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem($"Riftcaster {Updates.RunningVersion}") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Open dashboard", null, (_, _) => OpenDashboard(_dashboard));
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

        _icon.DoubleClick += (_, _) => OpenDashboard(_dashboard);
        _updates.ReadyChanged += (_, _) => ShowUpdateReady();
    }

    /// <summary>
    /// Whether Restart to update was chosen, rather than Quit.
    /// </summary>
    public bool RestartRequested { get; private set; }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Otherwise the icon can stay by the clock after quitting, until the mouse passes over it.
            _icon.ContextMenuStrip?.Dispose();
            _icon.Dispose();
            _logo.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Opens the admin dashboard in the default browser.
    /// </summary>
    /// <param name="dashboard">The dashboard's address.</param>
    public static void OpenDashboard(Uri dashboard) => Open(dashboard.ToString());

    private void ShowUpdateReady()
    {
        var version = _updates.Ready!.Version;
        _restartToUpdate.Text = $"Restart to update to {version}";
        _restartToUpdate.Visible = true;
        _icon.ShowBalloonTip(
            10_000,
            $"Riftcaster {version} is ready to install",
            "It will install the next time you quit Riftcaster. To install it now, right-click the Riftcaster icon and choose Restart to update.",
            ToolTipIcon.Info);
    }

    // Restarting stops the server, and the overlays with it, for a few seconds, so it's only ever
    // done from the menu; otherwise the update waits until Riftcaster quits.
    private void RestartToUpdate()
    {
        RestartRequested = true;
        ExitThread();
    }

    private static Icon LoadLogo(Size size)
    {
        using var stream = typeof(TrayContext).Assembly.GetManifestResourceStream("Riftcaster.ico")!;
        return new Icon(stream, size);
    }

    // UseShellExecute: Windows opens it as the Run box would, an address in the default browser
    // and a folder in File Explorer.
    private static void Open(string target) => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
}
