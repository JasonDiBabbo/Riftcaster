using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Riftcaster.Contracts;
using Riftcaster.Core.Players;

namespace Riftcaster.Core.Tests;

public sealed class JsonFilePlayersStoreTests : IDisposable
{
    private static readonly Card Jinx = new("1", "ogn-251-298", "Jinx, Loose Cannon", null, CardType.Legend, null, "Fury / Chaos", null, "Origins", "https://cards.test/1.png", Landscape: false);
    private static readonly Card VoidGate = new("2", "ogn-290-298", "Void Gate", null, CardType.Battlefield, null, "", null, "Origins", "https://cards.test/2.png", Landscape: true);

    private readonly FakeLogger<JsonFilePlayersStore> _logger = new();

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
                new Player("Mara", Jinx, null, null, Points: 5, GameWins: 1, Xp: 12),
                new Player("Dex", null, null, VoidGate, Points: 3, GameWins: 0, Xp: 9),
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

    [Fact]
    public void Load_NamesSavedByAnEarlierVersion_DropsThemAndKeepsTheRest()
    {
        // Before players held card snapshots, legends, champions and battlefields were saved as names.
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, """
            {"players":[{"name":"Mara","legend":"Jinx, Loose Cannon","champion":"Jinx, Rebel","battlefield":null,"points":5,"gameWins":1,"xp":12}],
             "teams":[{"points":3,"gameWins":1}]}
            """);

        var state = _store.Load();

        Assert.Equal(new PlayersState([new Player("Mara", null, null, null, 5, 1, 12)], [new TeamScore(3, 1)]), state);
        Assert.Equal(
            "Dropped 2 legend, champion or battlefield names saved by an earlier version; choose those cards again.",
            Assert.Single(_logger.Collector.GetSnapshot(), log => log.Level == LogLevel.Warning).Message);
    }

    // A fresh store reads from disk, proving the data isn't just held in memory.
    private JsonFilePlayersStore CreateStore() => new(_path, _logger);
}
