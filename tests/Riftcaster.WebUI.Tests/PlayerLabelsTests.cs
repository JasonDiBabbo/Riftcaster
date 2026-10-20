using Riftcaster.WebUI.Players;
using Riftcaster.Contracts;
using Riftcaster.Core.Match;

namespace Riftcaster.WebUI.Tests;

// How players are named and colored in each match mode.
public class PlayerLabelsTests
{
    private static MatchSettings Settings(MatchMode mode) =>
        new MatchService(new InMemoryMatchStore()).Settings with { Mode = mode };

    [Theory]
    [InlineData(MatchMode.OneVsOne, 0, "Left")]
    [InlineData(MatchMode.OneVsOne, 1, "Right")]
    [InlineData(MatchMode.TwoVsTwo, 1, "Team A")]
    [InlineData(MatchMode.TwoVsTwo, 2, "Team B")]
    [InlineData(MatchMode.FreeForAll3, 2, "Seat 3")]
    [InlineData(MatchMode.FreeForAll4, 3, "Seat 4")]
    public void Side(MatchMode mode, int seat, string expected)
    {
        Assert.Equal(expected, PlayerLabels.Side(Settings(mode), seat));
    }

    [Fact]
    public void Name_FallsBackToTheSeat()
    {
        var unnamed = new Player("", null, null, null, 0, 0, 0);

        Assert.Equal("Player 3", PlayerLabels.Name(unnamed, 2));
        Assert.Equal("Mara", PlayerLabels.Name(unnamed with { Name = "Mara" }, 2));
    }

    [Theory]
    [InlineData(MatchMode.OneVsOne, "1v1 · 2 players")]
    [InlineData(MatchMode.TwoVsTwo, "2v2 · 4 players")]
    [InlineData(MatchMode.FreeForAll3, "FFA 3 · 3 players")]
    [InlineData(MatchMode.FreeForAll4, "FFA 4 · 4 players")]
    public void Meta(MatchMode mode, string expected)
    {
        Assert.Equal(expected, PlayerLabels.Meta(Settings(mode)));
    }

    [Fact]
    public void Teams()
    {
        Assert.Equal("Team A", PlayerLabels.TeamName(0));
        Assert.Equal("Team B", PlayerLabels.TeamName(1));
        Assert.Equal([0, 0, 1, 1], Enumerable.Range(0, 4).Select(PlayerLabels.TeamOf));
        Assert.Equal([2, 3], PlayerLabels.SeatsOf(1));
    }

    [Fact]
    public void Hue_IsPerSeat_OrPerTeamInTeamMode()
    {
        Assert.Equal([250, 25, 85, 310], Enumerable.Range(0, 4).Select(seat => PlayerLabels.Hue(Settings(MatchMode.FreeForAll4), seat)));
        Assert.Equal([250, 250, 25, 25], Enumerable.Range(0, 4).Select(seat => PlayerLabels.Hue(Settings(MatchMode.TwoVsTwo), seat)));
        Assert.Equal(250, PlayerLabels.TeamHue(0));
        Assert.Equal(25, PlayerLabels.TeamHue(1));
    }
}
