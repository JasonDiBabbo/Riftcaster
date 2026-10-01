using System.Text.Json;
using Riftcaster.Contracts;

namespace Riftcaster.Core.Tests;

public class PlayersStateTests
{
    [Fact]
    public void Equals_SamePlayersAndTeamsInDifferentLists_AreEqual()
    {
        var first = new PlayersState([NewPlayer("Mara"), NewPlayer("Dex")], [new(3, 1), new(0, 0)]);
        var second = new PlayersState([NewPlayer("Mara"), NewPlayer("Dex")], [new(3, 1), new(0, 0)]);
        Assert.NotSame(first.Players, second.Players);

        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentPlayer_AreNotEqual()
    {
        var first = new PlayersState([NewPlayer("Mara"), NewPlayer("Dex")], [new(0, 0), new(0, 0)]);
        var second = new PlayersState([NewPlayer("Mara"), NewPlayer("Juno")], [new(0, 0), new(0, 0)]);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Equals_SamePlayersInDifferentOrder_AreNotEqual()
    {
        var first = new PlayersState([NewPlayer("Mara"), NewPlayer("Dex")], [new(0, 0), new(0, 0)]);
        var second = new PlayersState([NewPlayer("Dex"), NewPlayer("Mara")], [new(0, 0), new(0, 0)]);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Equals_DifferentTeamScore_AreNotEqual()
    {
        var first = new PlayersState([NewPlayer("Mara"), NewPlayer("Dex")], [new(3, 1), new(0, 0)]);
        var second = new PlayersState([NewPlayer("Mara"), NewPlayer("Dex")], [new(4, 1), new(0, 0)]);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Serialize_RoundTrips()
    {
        var state = new PlayersState(
            [new Player("Mara", "Jinx, Loose Cannon", null, null, Points: 5, GameWins: 1, Xp: 12)],
            [new TeamScore(3, 1)]);

        var json = JsonSerializer.Serialize(state, JsonSerializerOptions.Web);

        Assert.Equal(
            """{"players":[{"name":"Mara","legend":"Jinx, Loose Cannon","champion":null,"battlefield":null,"points":5,"gameWins":1,"xp":12}],"teams":[{"points":3,"gameWins":1}]}""",
            json);
        Assert.Equal(state, JsonSerializer.Deserialize<PlayersState>(json, JsonSerializerOptions.Web));
    }

    private static Player NewPlayer(string name) => new(name, null, null, null, 0, 0, 0);
}
