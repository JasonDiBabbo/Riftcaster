using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Riftcaster.Core.Diagnostics;

namespace Riftcaster.Core.Tests;

public sealed class LogFolderOpenerTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("riftcaster-tests-");

    private readonly string _folder;

    private readonly List<string> _opened = [];

    private readonly FakeLogger<LogFolderOpener> _logger = new();

    private bool _desktop = true;

    private Exception? _openFails;

    private readonly LogFolderOpener _opener;

    public LogFolderOpenerTests()
    {
        // A subfolder that doesn't exist yet, as in a copy that hasn't logged anything
        _folder = Path.Combine(_directory.FullName, "logs");
        _opener = new LogFolderOpener(
            new LogFolder(_folder),
            () => _desktop,
            path =>
            {
                if (_openFails is not null)
                {
                    throw _openFails;
                }

                _opened.Add(path);
            },
            _logger);
    }

    public void Dispose()
    {
        _directory.Delete(recursive: true);
    }

    [Fact]
    public void FolderPath_IsTheLogFolder()
    {
        Assert.Equal(_folder, _opener.FolderPath);
    }

    [Fact]
    public void CanOpen_FollowsTheDesktop()
    {
        Assert.True(_opener.CanOpen);

        _desktop = false;

        Assert.False(_opener.CanOpen);
    }

    [Fact]
    public void Open_AsksForTheLogFolderAndCreatesItFirst()
    {
        var result = _opener.Open();

        Assert.Equal(FolderOpenResult.Opened, result);
        Assert.Equal([_folder], _opened);
        Assert.True(Directory.Exists(_folder));
    }

    [Fact]
    public void Open_WithoutADesktop_DoesNothing()
    {
        _desktop = false;

        var result = _opener.Open();

        Assert.Equal(FolderOpenResult.NoDesktop, result);
        Assert.Empty(_opened);
        Assert.False(Directory.Exists(_folder));
    }

    [Theory]
    [MemberData(nameof(LaunchFailures))]
    public void Open_WhenTheFileManagerCantStart_ReturnsFailedAndLogsIt(Exception failure)
    {
        _openFails = failure;

        var result = _opener.Open();

        Assert.Equal(FolderOpenResult.Failed, result);
        Assert.Contains(_logger.Collector.GetSnapshot(), record => record.Level == LogLevel.Warning && ReferenceEquals(record.Exception, failure));
    }

    public static TheoryData<Exception> LaunchFailures() =>
    [
        new Win32Exception("No application is associated with this file."),
        new IOException("The folder can't be reached."),
        new UnauthorizedAccessException(),
        new InvalidOperationException(),
        new PlatformNotSupportedException(),
    ];

    [Fact]
    public void Open_WhenTheFolderCantBeCreated_ReturnsFailed()
    {
        // A file where the folder should be
        File.WriteAllText(_folder, "");

        var result = _opener.Open();

        Assert.Equal(FolderOpenResult.Failed, result);
        Assert.Empty(_opened);
    }
}
