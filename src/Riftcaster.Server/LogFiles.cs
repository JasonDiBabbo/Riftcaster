using Serilog;
using Serilog.Extensions.Logging;

namespace Riftcaster.Server;

/// <summary>
/// Writes everything the server logs to files as well as the console (#68): with no console, as
/// when the Windows launcher runs it, they're the only record.
/// </summary>
internal static class LogFiles
{
    /// <summary>
    /// The log folder's name, inside the data folder.
    /// </summary>
    public const string FolderName = "logs";

    /// <summary>
    /// Adds the log files: riftcaster-20261005.log and so on, a new one each day, in the data
    /// folder's logs folder. A week's files are kept; older ones are deleted. The levels in the
    /// Logging settings (appsettings.json) apply to them as to the console.
    /// </summary>
    /// <param name="logging">The app's logging.</param>
    /// <param name="dataDirectory">The saved-data folder (see <see cref="DataFolder"/>).</param>
    public static ILoggingBuilder AddLogFiles(this ILoggingBuilder logging, string dataDirectory)
    {
        var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose() // Everything it's given: the Logging settings have already filtered it.
            .WriteTo.File(Path.Combine(dataDirectory, FolderName, "riftcaster-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        // Not AddSerilog, which also lets every level through to Serilog, past the Logging settings.
        // Not AddProvider either: nothing disposes a provider added as an instance, so the file
        // would stay open. Created by the container, it's disposed, closing the file, when the
        // server is.
        logging.Services.AddSingleton<ILoggerProvider>(_ => new SerilogLoggerProvider(logger, dispose: true));
        return logging;
    }
}
