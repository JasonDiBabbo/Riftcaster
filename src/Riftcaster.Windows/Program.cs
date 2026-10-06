using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Riftcaster.Core.Network;
using Riftcaster.Server;

namespace Riftcaster.Windows;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize(); // Before any window, the error message included

        WebApplication server;
        try
        {
            server = Server.Program.CreateApp(args);
        }
        catch (Exception exception)
        {
            ShowStartupError(exception, logFolder: null); // Too early for a log file
            return;
        }

        using (server)
        {
            // With network access on, the server may listen on every interface, but the dashboard
            // always opens on this computer.
            var listenUrl = server.Services.GetRequiredService<NetworkAccess>().ListenUrls[0];
            var dashboard = new UriBuilder(listenUrl) { Host = "localhost" }.Uri;
            var logFolder = server.Services.GetRequiredService<LogFolder>().Path;

            // One copy at a time (#68): started again, it opens the running copy's dashboard instead of
            // failing to listen on the same port. Held until Main returns.
            using var instance = new Mutex(initiallyOwned: true, "Riftcaster", out var firstCopy);
            if (!firstCopy)
            {
                TrayContext.OpenDashboard(dashboard);
                return;
            }

            try
            {
                server.Start();
            }
            catch (Exception exception)
            {
                ShowStartupError(exception, logFolder); // The server has logged it, with the details
                return;
            }

            using var tray = new TrayContext(dashboard, logFolder);
            Application.Run(tray);

            server.StopAsync().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Says why Riftcaster couldn't start, since there's no console to say it in.
    /// </summary>
    /// <param name="exception">What went wrong.</param>
    /// <param name="logFolder">The log files' folder, or null if it failed before there was one.</param>
    private static void ShowStartupError(Exception exception, string? logFolder)
    {
        var message = $"Riftcaster couldn't start.\n\n{exception.Message}";

        // Most likely the console server (Riftcaster.Server), or another program, on the same port.
        if (exception.InnerException is AddressInUseException)
        {
            message += "\n\nIs Riftcaster already running in a console window? Close it, or start this one with a different --urls.";
        }

        if (logFolder is not null)
        {
            message += $"\n\nThe log has the details: {logFolder}";
        }

        MessageBox.Show(message, "Riftcaster", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
