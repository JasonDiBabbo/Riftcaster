using System.Diagnostics;

namespace Riftcaster.Windows;

/// <summary>
/// Riftcaster's icon by the clock, in place of a console window (#68): its menu opens the admin
/// dashboard or quits, and double-clicking it opens the dashboard.
/// </summary>
internal sealed class TrayContext : ApplicationContext
{
    private readonly Uri _dashboard;

    private readonly NotifyIcon _icon;

    /// <summary>
    /// Shows the icon.
    /// </summary>
    /// <param name="dashboard">The admin dashboard's address, on this computer.</param>
    public TrayContext(Uri dashboardAddress)
    {
        _dashboard = dashboardAddress;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open dashboard", null, (_, _) => OpenDashboard(_dashboard));
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
    // UseShellExecute: Windows opens the address in the default browser, as the Run box would.
    public static void OpenDashboard(Uri dashboard) => Process.Start(new ProcessStartInfo(dashboard.ToString()) { UseShellExecute = true });
}
