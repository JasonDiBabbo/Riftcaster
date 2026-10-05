using Microsoft.Extensions.Hosting;

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

        ApplicationConfiguration.Initialize();
        Application.Run(new Form1());

        server.StopAsync().GetAwaiter().GetResult();
    }
}
