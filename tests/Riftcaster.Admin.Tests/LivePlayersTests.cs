using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Riftcaster.Admin.Players;
using Riftcaster.Contracts;
using Riftcaster.Core.Cards;
using Riftcaster.Core.Match;
using Riftcaster.Core.Players;

namespace Riftcaster.Admin.Tests;

// The Players panel on the Live page: a cell per player (or per team in 2v2) with the scores in it.
public class LivePlayersTests : BunitContext
{
    private readonly MatchService _match = new(new InMemoryMatchStore());

    private readonly PlayersService _players;

    public LivePlayersTests()
    {
        _players = new PlayersService(new InMemoryPlayersStore(), _match);
        Services.AddSingleton(new CardCatalog(new InMemoryCardStore(), new InMemoryCardSource(), TimeProvider.System, NullLogger<CardCatalog>.Instance));
        Services.AddSingleton(_match);
        Services.AddSingleton(_players);
    }

    [Theory]
    [InlineData(MatchMode.OneVsOne, 2, "1v1 · 2 players")]
    [InlineData(MatchMode.FreeForAll3, 3, "FFA 3 · 3 players")]
    [InlineData(MatchMode.FreeForAll4, 4, "FFA 4 · 4 players")]
    public void HasOneCellPerPlayer(MatchMode mode, int cells, string meta)
    {
        SetMode(mode);

        var tiles = Render<LiveTiles>();

        Assert.Equal(cells, tiles.FindAll(".tile").Count);
        Assert.Equal(meta, tiles.Find(".meta").TextContent);
        Assert.Equal(cells.ToString(), tiles.Find(".tiles").GetAttribute("data-players"));
    }

    [Fact]
    public void TeamMode_HasOneCellPerTeam()
    {
        SetMode(MatchMode.TwoVsTwo);

        var tiles = Render<LiveTiles>();

        Assert.Equal(2, tiles.FindAll(".tile").Count);
        Assert.Equal(["Team A", "Team B"], tiles.FindAll(".name").Select(name => name.TextContent));
        Assert.NotNull(tiles.Find(".tiles").GetAttribute("data-teams"));
    }

    [Fact]
    public void PlayerCell_ShowsTheNameAndTheCardsReadOnly()
    {
        _players.UpdatePlayer(0, player => player with { Name = "Mara", Legend = TestCards.Legend("1", "Jinx, Loose Cannon") });
        SetMode(MatchMode.OneVsOne);

        var tiles = Render<LiveTiles>();

        var first = tiles.FindAll(".tile")[0];
        Assert.Equal("Mara", first.QuerySelector(".who")!.TextContent);
        Assert.Equal(["Jinx, Loose Cannon", "No champion", "No battlefield"], first.QuerySelectorAll(".chip").Select(chip => chip.TextContent));
        Assert.DoesNotContain("none", first.QuerySelectorAll(".chip")[0].ClassList);
        Assert.Contains("none", first.QuerySelectorAll(".chip")[1].ClassList);
        Assert.Equal("/match-setup", first.QuerySelector("a.edit")!.GetAttribute("href"));
        Assert.Equal("Player 2", tiles.FindAll(".tile")[1].QuerySelector(".who")!.TextContent); // Until a name is entered
    }

    [Fact]
    public void PlayerCell_ChangesThePlayersScores()
    {
        SetMode(MatchMode.OneVsOne);
        var tiles = Render<LiveTiles>();

        tiles.Find("button[aria-label='Increase Player 1 points']").Click();
        tiles.Find("button[aria-label='Increase Player 2 game wins']").Click();
        tiles.Find("button[aria-label='Increase Player 2 XP']").Click();

        Assert.Equal(1, _players.State.Players[0].Points);
        Assert.Equal(1, _players.State.Players[1].GameWins);
        Assert.Equal(1, _players.State.Players[1].Xp);
    }

    [Fact]
    public void TeamCell_ChangesTheTeamsScoresAndEachPlayersXp()
    {
        SetMode(MatchMode.TwoVsTwo);
        var tiles = Render<LiveTiles>();

        tiles.Find("button[aria-label='Increase Team A points']").Click();
        tiles.Find("button[aria-label='Increase Team B game wins']").Click();
        tiles.Find("button[aria-label='Increase Player 3 XP']").Click();

        Assert.Equal(1, _players.State.Teams[0].Points);
        Assert.Equal(1, _players.State.Teams[1].GameWins);
        Assert.Equal(1, _players.State.Players[2].Xp);
        Assert.Equal(["P1", "P2", "P3", "P4"], tiles.FindAll(".slot").Select(slot => slot.TextContent));
    }

    [Fact]
    public void TeamCell_ListsItsMembersByName()
    {
        _players.UpdatePlayer(0, player => player with { Name = "Mara" });
        _players.UpdatePlayer(1, player => player with { Name = "Dex" });
        SetMode(MatchMode.TwoVsTwo);

        var tiles = Render<LiveTiles>();

        Assert.Equal("Mara & Dex", tiles.FindAll(".members")[0].TextContent);
        Assert.Equal("/match-setup", tiles.FindAll("a.edit")[0].GetAttribute("href"));
    }

    [Fact]
    public void ResetScores_ClearsEveryScore()
    {
        SetMode(MatchMode.OneVsOne);
        _players.UpdatePlayer(0, player => player with { Points = 3, Xp = 2 });
        var tiles = Render<LiveTiles>();

        tiles.Find("button.button-secondary").Click();

        Assert.Equal(0, _players.State.Players[0].Points);
        Assert.Equal(0, _players.State.Players[0].Xp);
    }

    [Fact]
    public void ChangingTheMode_ChangesTheCells()
    {
        SetMode(MatchMode.OneVsOne);
        var tiles = Render<LiveTiles>();
        Assert.Equal(2, tiles.FindAll(".tile").Count);

        SetMode(MatchMode.FreeForAll4);

        tiles.WaitForAssertion(() => Assert.Equal(4, tiles.FindAll(".tile").Count));
    }

    private void SetMode(MatchMode mode) => _match.Update(settings => settings with { Mode = mode });
}
