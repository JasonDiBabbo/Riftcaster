using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Riftcaster.Core.Diagnostics;
using Riftcaster.Core.Network;
using Riftcaster.Core.Settings;
using Riftcaster.Core.Startup;
using Riftcaster.Core.Updates;
using Riftcaster.Server;
using Velopack;

namespace Riftcaster.Windows;

static class Program
{
    /// <summary>
    /// The launch option that decides whether the web interface opens on starting, for that run only. Without it, the
    /// Settings page's choice does, which is to open it unless set: started from the Start menu or after installing,
    /// Riftcaster shows its web interface rather than only an icon by the clock.
    /// </summary>
    public const string OpenWebUISetting = "Launcher:OpenWebUI";

    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    /// <returns>
    /// 1 if Riftcaster couldn't start; 0 otherwise, including when it was already running.
    /// </returns>
    [STAThread]
    static int Main(string[] args)
    {
        // First of all: installing, updating and uninstalling (#62) run this exe with arguments of
        // their own, which this handles, then exits, before a server or tray is started.
        //
        // Not applying a downloaded update on the next start, which Velopack does by default: whether it's
        // installed is the updates setting's to say (see Updates.InstallOnExit), and Ask first and Off would
        // otherwise be overridden by simply starting Riftcaster again.
        //
        // Uninstalling also takes the sign-in entry (Start with Windows) with it, so nothing is left pointing at an exe that's gone.
        VelopackApp.Build()
            .SetAutoApplyOnStartup(false)
            .OnBeforeUninstallFastCallback(_ => RunEntry.Remove())
            .Run();

        ApplicationConfiguration.Initialize(); // Before any window, the error message included

        WebApplication server;
        try
        {
            server = Server.Program.CreateApp(args);
        }
        catch (Exception exception)
        {
            ShowStartupError(exception, logFolder: null); // Too early for a log file
            return 1;
        }

        try
        {
            // With network access on, the server accepts connections from other devices too, but the
            // launcher always opens Riftcaster through localhost, on this computer.
            // The address is worked out each time it's used: the port can be switched from the web interface
            // while Riftcaster runs.
            var network = server.Services.GetRequiredService<NetworkAccess>();
            Uri webUI() => new UriBuilder(network.ListenUrls[0]) { Host = "localhost" }.Uri;
            var logFolder = server.Services.GetRequiredService<LogFolder>().Path;

            // One copy at a time (#68): started again, it opens the running copy's web interface instead of
            // failing to listen on the same port. Held until Main returns.
            using var instance = new Mutex(initiallyOwned: true, "Riftcaster", out var firstCopy);
            if (!firstCopy)
            {
                TrayContext.OpenWebUI(webUI());
                return 0;
            }

            if (!StartServer(ref server, args))
            {
                return 1;
            }

            network = server.Services.GetRequiredService<NetworkAccess>(); // Another server if it started on another port

            var configuration = server.Services.GetRequiredService<IConfiguration>();
            var updateService = server.Services.GetRequiredService<UpdateService>();
            using var updates = new Updates(
                configuration[Updates.Setting],
                updateService,
                server.Services.GetRequiredService<ILoggerFactory>().CreateLogger<Updates>());
            using var tray = new TrayContext(webUI, logFolder, updateService);
            updates.Start(tray.RestartToUpdate);

            // An entry that starts Riftcaster at sign-in only makes sense for an installed copy: a build folder is
            // replaced, and an unzipped one can be moved.
            var startup = server.Services.GetRequiredService<StartupService>();
            if (updates.IsInstalled)
            {
                startup.Attach(new RunEntry());
            }
            else
            {
                startup.Unavailable(StartupAvailability.NotInstalled);
            }

            // A launch option decides for this run; otherwise the Settings page's choice does.
            if (configuration.GetValue<bool?>(OpenWebUISetting) ?? startup.OpenWebUI)
            {
                TrayContext.OpenWebUI(webUI());
            }

            // Ctrl+C in the console that started it (or a shutdown signal) stops the server, which would
            // otherwise leave the tray running with nothing behind it, until Quit.
            using var stopping = server.Lifetime.ApplicationStopping.Register(Application.Exit);

            Application.Run(tray);

            server.StopAsync().GetAwaiter().GetResult();

            // Restarted with the same arguments, such as --urls, but without another browser tab:
            // the one from before the update is usually still open.
            updates.InstallOnExit(restart: tray.RestartRequested, restartArgs: RestartArguments(args));
        }
        finally
        {
            ((IDisposable)server).Dispose();
        }

        return 0;
    }

    /// <summary>
    /// The arguments to start Riftcaster again with after an update: the ones it was started with, but not opening
    /// another web interface tab, since the one from before the update is usually still open.
    /// </summary>
    /// <remarks>
    /// Any <see cref="OpenWebUISetting"/> it was started with is replaced, not added to: the restarted copy gets these
    /// same arguments for its own restart, and each would otherwise add one more.
    /// </remarks>
    /// <param name="args">The arguments Riftcaster was started with.</param>
    private static string[] RestartArguments(string[] args)
    {
        var option = $"--{OpenWebUISetting}";
        var kept = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith($"{option}=", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (args[i].Equals(option, StringComparison.OrdinalIgnoreCase))
            {
                i++; // Given as two arguments: the value follows
                continue;
            }

            kept.Add(args[i]);
        }

        return [.. kept, $"{option}=false"];
    }

    /// <summary>
    /// Starts the server. If its port is taken by another program, asks which port to use instead, and
    /// starts a new server on it.
    /// </summary>
    /// <param name="server">The server to start, and the one that started: another, if the port changed.</param>
    /// <param name="args">The arguments it was created with.</param>
    /// <returns>Whether a server is running. If not, why has been shown.</returns>
    private static bool StartServer(ref WebApplication server, string[] args)
    {
        while (true)
        {
            var logFolder = server.Services.GetRequiredService<LogFolder>().Path;
            var network = server.Services.GetRequiredService<NetworkAccess>();
            var origin = server.Services.GetRequiredService<PortOrigin>();
            try
            {
                server.Start();
                return true;
            }
            catch (Exception exception)
            {
                // A --urls option is the port someone asked for, so Riftcaster doesn't pick another for them.
                if (!PortConflict.IsPortInUse(exception)
                    || ListenPort.HttpPort(network.ListenUrls) is not { } taken
                    || origin.Source == PortSource.LaunchOption)
                {
                    ShowStartupError(exception, logFolder); // The server has logged it, with the details
                    return false;
                }

                using var dialog = new PortInUseDialog(taken, ListenPort.NextFree(taken, network.Enabled), network.Enabled);
                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return false;
                }

                ((IDisposable)server).Dispose();
                try
                {
                    // For this run only, which Remember turns into the port to use from now on once it's running.
                    server = Server.Program.CreateApp([.. args, $"--{Server.Program.PortSetting}={dialog.Port}"]);
                }
                catch (Exception creating)
                {
                    ShowStartupError(creating, logFolder: null);
                    return false;
                }

                if (dialog.Remember)
                {
                    server.Services.GetRequiredService<AppSettingsService>().ChoosePort(dialog.Port);
                }
            }
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
            message += "\n\nIs Riftcaster already running in a console window? Close it, or start this one with a different --urls option.";
        }

        if (logFolder is not null)
        {
            message += $"\n\nCheck the log folder for more information: {logFolder}";
        }

        MessageBox.Show(message, "Riftcaster", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
