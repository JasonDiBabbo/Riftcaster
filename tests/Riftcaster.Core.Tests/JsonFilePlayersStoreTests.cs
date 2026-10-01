using Microsoft.Extensions.Logging.Abstractions;
using Riftcaster.Contracts;
using Riftcaster.Core.Players;

namespace Riftcaster.Core.Tests;

public sealed class JsonFilePlayersStoreTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("riftcaster-tests-");

    private readonly string _path;

    private readonly JsonFilePlayersStore _store;

    public JsonFilePlayersStoreTests()
    {
        // A subfolder that doesn't exist yet, to check that Save creates it
        _path = Path.Combine(_directory.FullName, "data", "players.json");
        _store = CreateStore();
    }

    public void Dispose()
    {
        _directory.Delete(recursive: true);
    }

    [Fact]
    public void Load_MissingFile_ReturnsNull()
    {
        var state = _store.Load();

        Assert.Null(state);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var state = new PlayersState(
            [
                new Player("Mara", "Jinx, Loose Cannon", "Jinx, Rebel", null, Points: 5, GameWins: 1, Xp: 12),
                new Player("Dex", null, null, "Void Gate", Points: 3, GameWins: 0, Xp: 9),
            ],
            [new TeamScore(3, 1), new TeamScore(0, 0)]);
        _store.Save(state);

        var loaded = CreateStore().Load();

        Assert.Equal(state, loaded);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{"players":[],"teams":null}""")]
    [InlineData("""{"players":[{"name":null,"legend":null,"champion":null,"battlefield":null,"points":0,"gameWins":0,"xp":0}],"teams":[]}""")]
    [InlineData("""{"players":[{"name":"Mara","legend":null,"champion":null,"battlefield":null,"points":0,"gameWins":0}],"teams":[]}""")]
    public void Load_InvalidFile_ReturnsNullAndKeepsBackup(string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, contents);

        var state = _store.Load();

        Assert.Null(state);
        Assert.False(File.Exists(_path));
        Assert.Equal(contents, File.ReadAllText(_path + ".bad"));
    }

    // A fresh store reads from disk, proving the data isn't just held in memory.
    private JsonFilePlayersStore CreateStore() => new(_path, NullLogger<JsonFilePlayersStore>.Instance);
}
