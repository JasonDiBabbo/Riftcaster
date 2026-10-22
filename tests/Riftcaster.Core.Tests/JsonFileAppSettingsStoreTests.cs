using Microsoft.Extensions.Logging.Abstractions;
using Riftcaster.Contracts;
using Riftcaster.Core.Match;
using Riftcaster.Core.Settings;

namespace Riftcaster.Core.Tests;

public sealed class JsonFileAppSettingsStoreTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("riftcaster-tests-");

    private readonly string _path;

    private readonly JsonFileAppSettingsStore _store;

    public JsonFileAppSettingsStoreTests()
    {
        // A subfolder that doesn't exist yet, to check that Save creates it
        _path = Path.Combine(_directory.FullName, "data", "settings.json");
        _store = CreateStore();
    }

    public void Dispose()
    {
        _directory.Delete(recursive: true);
    }

    [Fact]
    public void Load_MissingFile_ReturnsNull()
    {
        Assert.Null(_store.Load());
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var settings = new AppSettings { Logging = new LoggingSettings { Detail = LogDetail.Detailed } };
        _store.Save(settings);

        var loaded = CreateStore().Load();

        Assert.Equal(settings, loaded);
    }

    [Fact]
    public void Save_WritesTheDetailByName()
    {
        _store.Save(new AppSettings { Logging = new LoggingSettings { Detail = LogDetail.WarningsOnly } });

        Assert.Contains("\"WarningsOnly\"", File.ReadAllText(_path));
    }

    // A file saved before a setting existed has no part for it: it must still load, with that setting at
    // its default, or every new version would wipe the settings people had chosen.
    [Theory]
    [InlineData("{}")]
    [InlineData("""{"logging":{}}""")]
    public void Load_FileWithoutSomeSettings_UsesTheirDefaults(string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, contents);

        var settings = _store.Load();

        Assert.Equal(new AppSettings(), settings);
        Assert.True(File.Exists(_path));
    }

    [Fact]
    public void SaveThenLoad_RoundTripsTheMatchDefaults()
    {
        var settings = new AppSettings { MatchDefaults = MatchRules.Default with { Mode = MatchMode.TwoVsTwo, Format = MatchFormat.BestOf1, DurationMinutes = 30 } };
        _store.Save(settings);

        var loaded = CreateStore().Load();

        Assert.Equal(settings, loaded);
    }

    // Until someone chooses them the defaults are the design's, which a file says by not having them.
    [Theory]
    [InlineData("""{"logging":{"detail":"Normal"}}""")]
    [InlineData("""{"logging":{"detail":"Normal"},"matchDefaults":null}""")]
    public void Load_FileWithoutMatchDefaults_HasNone(string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, contents);

        var settings = _store.Load();

        Assert.NotNull(settings);
        Assert.Null(settings.MatchDefaults);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("""{"logging":null}""")]
    [InlineData("""{"logging":{"detail":"Chatty"}}""")]
    [InlineData("""{"matchDefaults":{"pointsToWin":8,"format":"BestOf5","mode":"OneVsOne","durationMinutes":50,"allowOvertime":true}}""")]
    [InlineData("""{"matchDefaults":{"pointsToWin":8}}""")]
    public void Load_InvalidFile_ReturnsNullAndKeepsBackup(string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, contents);

        var settings = _store.Load();

        Assert.Null(settings);
        Assert.False(File.Exists(_path));
        Assert.Equal(contents, File.ReadAllText(_path + ".bad"));
    }

    // A fresh store reads from disk, proving the data isn't just held in memory.
    private JsonFileAppSettingsStore CreateStore() => new(_path, NullLogger<JsonFileAppSettingsStore>.Instance);
}
