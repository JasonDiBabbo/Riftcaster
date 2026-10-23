namespace Riftcaster.Server.Tests;

public sealed class SavedPortTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("riftcaster-tests-");

    private readonly string _file;

    public SavedPortTests()
    {
        _file = Path.Combine(_directory.FullName, "settings.json");
    }

    public void Dispose()
    {
        _directory.Delete(recursive: true);
    }

    [Fact]
    public void Read_WithNoFile_IsNone()
    {
        Assert.Null(SavedPort.Read(_file));
    }

    [Theory]
    [InlineData("""{"server":{"port":5070}}""", 5070)]
    [InlineData("""{"logging":{"detail":"Normal"},"server":{"port":1024}}""", 1024)]
    [InlineData("""{"Server":{"Port":5071}}""", 5071)] // Saved by hand, in another case: the settings service reads it too
    [InlineData("""{"server":{"port":65535}}""", 65535)]
    public void Read_FindsTheChosenPort(string json, int expected)
    {
        File.WriteAllText(_file, json);

        Assert.Equal(expected, SavedPort.Read(_file));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"server":{}}""")]
    [InlineData("""{"server":{"port":null}}""")]
    [InlineData("""{"server":null}""")]
    [InlineData("""{"server":5070}""")]
    [InlineData("""{"server":{"port":"5070"}}""")]
    [InlineData("""{"server":{"port":5070.5}}""")]
    [InlineData("""{"server":{"port":80}}""")] // Below what can be chosen
    [InlineData("""{"server":{"port":70000}}""")]
    [InlineData("""{"server":{"port":99999999999}}""")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("not json")]
    [InlineData("")]
    public void Read_WithNoUsablePort_IsNone(string json)
    {
        File.WriteAllText(_file, json);

        Assert.Null(SavedPort.Read(_file));
    }

    // It only looks. A file the settings service can't read is moved aside and logged there, where the
    // log is: reading it here first must not do that, or the service would find nothing and say nothing.
    [Fact]
    public void Read_LeavesAFileItCantUseWhereItIs()
    {
        File.WriteAllText(_file, "not json");

        SavedPort.Read(_file);

        Assert.True(File.Exists(_file));
        Assert.Equal("not json", File.ReadAllText(_file));
        Assert.False(File.Exists(_file + ".bad"));
    }

    [Fact]
    public void Read_WithAFileThatIsLocked_IsNone()
    {
        // Only Windows stops another program reading a file that is open: elsewhere the lock is advisory.
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        File.WriteAllText(_file, """{"server":{"port":5070}}""");
        using var locked = new FileStream(_file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        Assert.Null(SavedPort.Read(_file));
    }
}
