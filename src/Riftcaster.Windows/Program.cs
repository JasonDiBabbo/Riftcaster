using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Riftcaster.Core.Network;

namespace Riftcaster.Windows;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        using var server = Server.Program.CreateApp(args);
        server.Start();

        // With network access on, the server may listen on every interface, but the dashboard
        // always opens on this computer.
        var listenUrl = server.Services.GetRequiredService<NetworkAccess>().ListenUrls[0];
        var dashboard = new UriBuilder(listenUrl) { Host = "localhost" }.Uri;

        ApplicationConfiguration.Initialize();
        using var tray = new TrayContext(dashboard);
        Application.Run(tray);

        server.StopAsync().GetAwaiter().GetResult();
    }
}
