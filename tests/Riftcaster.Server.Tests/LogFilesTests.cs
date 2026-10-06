using Microsoft.Extensions.Logging;

namespace Riftcaster.Server.Tests;

public sealed class LogFilesTests : IDisposable
{
    private readonly string _dataDirectory = Directory.CreateTempSubdirectory("riftcaster-tests-").FullName;

    public void Dispose() => Directory.Delete(_dataDirectory, recursive: true);

    [Fact]
    public void AddLogFiles_WritesADailyFileInTheDataFoldersLogsFolder()
    {
        using (var loggers = LoggerFactory.Create(logging => logging.AddLogFiles(_dataDirectory)))
        {
            loggers.CreateLogger("Riftcaster.Server").LogWarning("Something to look at: {Thing}.", 42);
        }

        var file = Assert.Single(Directory.GetFiles(Path.Combine(_dataDirectory, LogFiles.FolderName)));
        Assert.Equal($"riftcaster-{DateTime.Now:yyyyMMdd}.log", Path.GetFileName(file));
        Assert.EndsWith("[WRN] Riftcaster.Server: Something to look at: 42." + Environment.NewLine, File.ReadAllText(file));
    }

    [Fact]
    public void AddLogFiles_FollowsTheLoggingLevels()
    {
        // As appsettings.json's Logging settings do: Information and up, but only warnings from
        // Microsoft.AspNetCore.
        using (var loggers = LoggerFactory.Create(logging => logging
            .SetMinimumLevel(LogLevel.Information)
            .AddFilter("Microsoft.AspNetCore", LogLevel.Warning)
            .AddLogFiles(_dataDirectory)))
        {
            loggers.CreateLogger("Riftcaster.Server").LogDebug("Hidden debug.");
            loggers.CreateLogger("Riftcaster.Server").LogInformation("Shown information.");
            loggers.CreateLogger("Microsoft.AspNetCore.Routing").LogInformation("Hidden framework information.");
        }

        var log = File.ReadAllText(Assert.Single(Directory.GetFiles(Path.Combine(_dataDirectory, LogFiles.FolderName))));
        Assert.Contains("Shown information.", log);
        Assert.DoesNotContain("Hidden", log);
    }
}
