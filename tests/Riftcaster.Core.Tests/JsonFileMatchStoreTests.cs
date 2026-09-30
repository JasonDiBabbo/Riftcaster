using Microsoft.Extensions.Logging.Abstractions;
using Riftcaster.Contracts;
using Riftcaster.Core.Match;

namespace Riftcaster.Core.Tests;

public sealed class JsonFileMatchStoreTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("riftcaster-tests-");

    private readonly string _path;

    private readonly JsonFileMatchStore _store;

    public JsonFileMatchStoreTests()
    {
        // A subfolder that doesn't exist yet, to check that Save creates it
        _path = Path.Combine(_directory.FullName, "data", "match.json");
        _store = CreateStore();
    }

    public void Dispose()
    {
        _directory.Delete(recursive: true);
    }

    [Fact]
    public void Load_MissingFile_ReturnsNull()
    {
        var settings = _store.Load();

        Assert.Null(settings);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var settings = MatchRules.Default with { Mode = MatchMode.TwoVsTwo, Format = MatchFormat.BestOf1 };
        _store.Save(settings);

        var loaded = CreateStore().Load();

        Assert.Equal(settings, loaded);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{"pointsToWin":8,"format":"BestOf5","mode":"OneVsOne","durationMinutes":50,"allowOvertime":true}""")]
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
    private JsonFileMatchStore CreateStore() => new(_path, NullLogger<JsonFileMatchStore>.Instance);
}
