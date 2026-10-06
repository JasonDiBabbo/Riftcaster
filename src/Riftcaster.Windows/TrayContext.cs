using System.Diagnostics;

namespace Riftcaster.Windows;

/// <summary>
/// Riftcaster's icon by the clock, in place of a console window (#68). Its menu has Open
/// dashboard, Open log folder and Quit; double-clicking the icon also opens the dashboard.
/// </summary>
internal sealed class TrayContext : ApplicationContext
{
    private readonly Uri _dashboard;

    private readonly NotifyIcon _icon;

    /// <summary>
    /// Shows the icon.
    /// </summary>
    /// <param name="dashboard">The admin dashboard's address, on this computer.</param>
    /// <param name="logFolder">The folder where log files are stored.</param>
    public TrayContext(Uri dashboard, string logFolder)
    {
        _dashboard = dashboard;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open dashboard", null, (_, _) => OpenDashboard(_dashboard));
        menu.Items.Add("Open log folder", null, (_, _) => Open(logFolder));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => ExitThread());

        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Application, // A placeholder icon until a custom one is made.
            Text = "Riftcaster",
            Visible = true,
            ContextMenuStrip = menu,
        };

        _icon.DoubleClick += (_, _) => OpenDashboard(_dashboard);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Otherwise the icon can stay by the clock after quitting, until the mouse passes over it.
            _icon.ContextMenuStrip?.Dispose();
            _icon.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Opens the admin dashboard in the default browser.
    /// </summary>
    /// <param name="dashboard">The dashboard's address.</param>
    public static void OpenDashboard(Uri dashboard) => Open(dashboard.ToString());

    // UseShellExecute: Windows opens it as the Run box would, an address in the default browser
    // and a folder in File Explorer.
    private static void Open(string target) => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
}
