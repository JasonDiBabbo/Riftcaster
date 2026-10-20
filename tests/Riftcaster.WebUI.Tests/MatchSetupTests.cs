using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Riftcaster.WebUI.Match;
using Riftcaster.WebUI.Players;
using Riftcaster.Contracts;
using Riftcaster.Core.Cards;
using Riftcaster.Core.Match;
using Riftcaster.Core.Players;

namespace Riftcaster.WebUI.Tests;

// The Match setup page's two sections: the match's settings, and each player's name and cards.
public class MatchSetupTests : BunitContext
{
    private readonly MatchService _match = new(new InMemoryMatchStore());

    private readonly PlayersService _players;

    public MatchSetupTests()
    {
        _players = new PlayersService(new InMemoryPlayersStore(), _match);
        Services.AddSingleton(new CardCatalog(new InMemoryCardStore(), new InMemoryCardSource(), TimeProvider.System, NullLogger<CardCatalog>.Instance));
        Services.AddSingleton(_match);
        Services.AddSingleton(_players);
    }

    [Fact]
    public void Settings_ChooseTheModeAndFormat()
    {
        var settings = Render<MatchSettingsSection>();

        settings.FindAll("[aria-label='Mode'] button").Single(button => button.TextContent.Trim() == "2v2").Click();
        settings.FindAll("[aria-label='Format'] button").Single(button => button.TextContent.Trim() == "Best of 1").Click();

        Assert.Equal(MatchMode.TwoVsTwo, _match.Settings.Mode);
        Assert.Equal(MatchFormat.BestOf1, _match.Settings.Format);
    }

    [Fact]
    public void Settings_ChangeThePointsAndDuration()
    {
        var settings = Render<MatchSettingsSection>();

        settings.Find("button[aria-label='Increase Points to win']").Click();
        settings.Find("button[aria-label='Increase Duration in minutes']").Click();

        Assert.Equal(MatchService.MinPointsToWin + 1, _match.Settings.PointsToWin);
        Assert.Equal(55, _match.Settings.DurationMinutes); // In steps of 5
    }

    [Fact]
    public void Settings_ToggleOvertime()
    {
        var before = _match.Settings.AllowOvertime;
        var settings = Render<MatchSettingsSection>();

        settings.Find("button[role='switch']").Click();

        Assert.Equal(!before, _match.Settings.AllowOvertime);
    }

    [Fact]
    public void Settings_FollowChangesMadeElsewhere()
    {
        var settings = Render<MatchSettingsSection>();

        _match.Update(match => match with { Mode = MatchMode.FreeForAll3 });

        settings.WaitForAssertion(() =>
            Assert.Equal("FFA 3", settings.Find("[aria-label='Mode'] button.selected").TextContent.Trim()));
    }

    [Theory]
    [InlineData(MatchMode.OneVsOne, 2)]
    [InlineData(MatchMode.FreeForAll3, 3)]
    [InlineData(MatchMode.FreeForAll4, 4)]
    public void Players_HaveOneRowEach(MatchMode mode, int rows)
    {
        _match.Update(settings => settings with { Mode = mode });

        var section = Render<PlayerSetupSection>();

        Assert.Equal(rows, section.FindAll(".row").Count);
        Assert.Equal($"{(mode == MatchMode.OneVsOne ? "1v1" : mode == MatchMode.FreeForAll3 ? "FFA 3" : "FFA 4")} · {rows} players", section.Find(".meta").TextContent);
        Assert.Equal(["Player 1", "Player 2"], section.FindAll(".slot").Take(2).Select(slot => slot.TextContent));
    }

    [Fact]
    public void Players_InTeamModeAreLabeledByTeam()
    {
        _match.Update(settings => settings with { Mode = MatchMode.TwoVsTwo });

        var section = Render<PlayerSetupSection>();

        Assert.Equal(
            ["Team A · Player 1", "Team A · Player 2", "Team B · Player 3", "Team B · Player 4"],
            section.FindAll(".slot").Select(slot => slot.TextContent));
    }

    [Fact]
    public void Row_SetsThePlayersName()
    {
        _match.Update(settings => settings with { Mode = MatchMode.OneVsOne });
        var section = Render<PlayerSetupSection>();

        section.FindAll(".name input")[0].Change("  Mara ");

        Assert.Equal("Mara", _players.State.Players[0].Name); // Trimmed
    }

    [Fact]
    public void Row_SwapsSeats()
    {
        _match.Update(settings => settings with { Mode = MatchMode.OneVsOne });
        _players.UpdatePlayer(0, player => player with { Name = "Mara" });
        _players.UpdatePlayer(1, player => player with { Name = "Dex" });
        var section = Render<PlayerSetupSection>();

        // The other player is listed by name, and choosing them swaps the two.
        var swap = section.Find("select[aria-label='Swap player 1 with']");
        Assert.Equal(["Swap with…", "Dex"], swap.QuerySelectorAll("option").Select(option => option.TextContent));
        swap.Change("1");

        Assert.Equal("Dex", _players.State.Players[0].Name);
        Assert.Equal("Mara", _players.State.Players[1].Name);
    }

    [Fact]
    public void Row_ChoosesTheCards()
    {
        var jinx = TestCards.Legend("1", "Jinx, Loose Cannon");
        _match.Update(settings => settings with { Mode = MatchMode.OneVsOne });
        _players.UpdatePlayer(0, player => player with { Legend = jinx });
        var section = Render<PlayerSetupSection>();

        // The legend is shown as chosen, and clearing it clears it.
        section.Find(".legend select").Change("");

        Assert.Null(_players.State.Players[0].Legend);
    }
}
