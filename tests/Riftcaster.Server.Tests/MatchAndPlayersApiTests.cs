using Microsoft.Extensions.DependencyInjection;
using Riftcaster.Contracts;
using Riftcaster.Core.Cards;

namespace Riftcaster.Server.Tests;

// Its own test server (a fixture per class): these tests change the match and players. Each test
// sets up what it relies on first, since they share that state.
public class MatchAndPlayersApiTests(RiftcasterWebApplicationFactory factory) : IClassFixture<RiftcasterWebApplicationFactory>
{
    private static readonly Card Jinx = TestCard("legend-jinx", "Jinx, Loose Cannon", CardType.Legend);

    private static readonly Card JinxUnit = TestCard("unit-jinx", "Jinx, Rebel", CardType.Unit, CardSupertype.Champion);

    private static readonly Card Forge = TestCard("bf-forge", "The Grand Forge", CardType.Battlefield);

    private readonly RestApiClient _api = new(factory.CreateClient());

    [Fact]
    public async Task PatchMatch_ChangesOnlyTheFieldsSent()
    {
        await _api.SendAsync<MatchSettings>(HttpMethod.Patch, "/api/match", new { pointsToWin = 8, allowOvertime = false });

        var settings = await _api.SendAsync<MatchSettings>(HttpMethod.Patch, "/api/match", """{ "pointsToWin": 11, "format": "BestOf1" }""");

        Assert.Equal(11, settings.PointsToWin);
        Assert.Equal(MatchFormat.BestOf1, settings.Format);
        Assert.False(settings.AllowOvertime); // Not sent, so kept
        Assert.Equal(settings, await _api.GetAsync<MatchSettings>("/api/match"));
    }

    [Theory]
    [InlineData("""{ "pointsToWin": 7 }""", "pointsToWin")]
    [InlineData("""{ "durationMinutes": 181 }""", "durationMinutes")]
    [InlineData("""{ "format": 5 }""", "format")]
    [InlineData("""{ "mode": 9 }""", "mode")]
    public async Task PatchMatch_InvalidValue_Is400(string body, string field)
    {
        var errors = await _api.InvalidAsync(HttpMethod.Patch, "/api/match", body);

        Assert.Contains(field, errors.Keys);
    }

    [Fact]
    public async Task PatchMatch_UnknownEnumName_Is400()
    {
        var response = await _api.SendRawAsync(HttpMethod.Patch, "/api/match", """{ "mode": "OneVsTen" }""");

        Assert.Equal(400, (int)response.StatusCode);
    }

    [Fact]
    public async Task PatchPlayer_SetsDetailsAndCards()
    {
        await LoadCardsAsync();

        var player = await _api.SendAsync<Player>(HttpMethod.Patch, "/api/players/2", new
        {
            name = "Mara",
            legendId = Jinx.Id,
            championId = JinxUnit.Id,
            battlefieldId = Forge.Id,
            xp = 12,
        });

        Assert.Equal("Mara", player.Name);
        Assert.Equal(Jinx, player.Legend);
        Assert.Equal(JinxUnit, player.Champion);
        Assert.Equal(Forge, player.Battlefield);
        Assert.Equal(12, player.Xp);
        Assert.Equal(player, (await _api.GetAsync<PlayersState>("/api/players")).Players[1]); // Seat 2 is the second
        Assert.Equal(player, await _api.GetAsync<Player>("/api/players/2"));

        var cleared = await _api.SendAsync<Player>(HttpMethod.Patch, "/api/players/2", new { legendId = "" });
        Assert.Null(cleared.Legend);
        Assert.Equal(JinxUnit, cleared.Champion); // Not sent, so kept
    }

    [Fact]
    public async Task PatchPlayer_InvalidCards_Is400()
    {
        await LoadCardsAsync();

        var errors = await _api.InvalidAsync(HttpMethod.Patch, "/api/players/1", new
        {
            legendId = Forge.Id, // A battlefield
            championId = "no-such-card",
        });

        Assert.Equal(["The Grand Forge isn't a legend."], errors["legendId"]);
        Assert.Contains("No card with id 'no-such-card'", errors["championId"][0]);
    }

    [Fact]
    public async Task PatchPlayer_ScoresAboveTheMatchLimits_Is400()
    {
        await _api.SendAsync<MatchSettings>(HttpMethod.Patch, "/api/match", new { pointsToWin = 8, format = "BestOf1" });

        var errors = await _api.InvalidAsync(HttpMethod.Patch, "/api/players/1", new { points = 9, gameWins = 2 });

        Assert.Equal(["At most 8, the match's points to win."], errors["points"]);
        Assert.Equal(["At most 1, the most a player can win in this format."], errors["gameWins"]);
    }

    [Fact]
    public async Task PatchPlayer_OutsideTheFixedLimits_Is400()
    {
        // Checked before the match's own limits, so these are the only errors.
        var errors = await _api.InvalidAsync(HttpMethod.Patch, "/api/players/1", new { points = 99, xp = 100 });

        Assert.Equal(["points", "xp"], errors.Keys.Order());
    }

    [Fact]
    public async Task AdjustPlayer_AddsAndStopsAtTheLimits()
    {
        await _api.SendAsync<MatchSettings>(HttpMethod.Patch, "/api/match", new { pointsToWin = 8 });
        await _api.SendAsync<Player>(HttpMethod.Patch, "/api/players/3", new { points = 7, xp = 0 });

        var once = await _api.SendAsync<Player>(HttpMethod.Post, "/api/players/3/adjust", new { points = 1, xp = 2 });
        var again = await _api.SendAsync<Player>(HttpMethod.Post, "/api/players/3/adjust", new { points = 1 });

        Assert.Equal(8, once.Points);
        Assert.Equal(2, once.Xp);
        Assert.Equal(8, again.Points); // At the points to win already
    }

    [Theory]
    [InlineData("GET", "/api/players/0")]
    [InlineData("PATCH", "/api/players/5")]
    [InlineData("POST", "/api/players/5/adjust")]
    public async Task UnknownSeat_Is404(string method, string path)
    {
        var detail = await _api.NotFoundAsync(new HttpMethod(method), path, method == "GET" ? null : new { points = 1 });

        Assert.EndsWith("Seats are 1 to 4.", detail);
    }

    [Fact]
    public async Task Swap_SwapsTwoSeats()
    {
        await _api.SendAsync<Player>(HttpMethod.Patch, "/api/players/1", new { name = "Ana" });
        await _api.SendAsync<Player>(HttpMethod.Patch, "/api/players/4", new { name = "Bo" });

        var state = await _api.SendAsync<PlayersState>(HttpMethod.Post, "/api/players/swap", new { first = 1, second = 4 });

        Assert.Equal("Bo", state.Players[0].Name);
        Assert.Equal("Ana", state.Players[3].Name);
        Assert.Contains("second", (await _api.InvalidAsync(HttpMethod.Post, "/api/players/swap", new { first = 1, second = 5 })).Keys);
    }

    [Fact]
    public async Task ResetScores_KeepsNames()
    {
        await _api.SendAsync<MatchSettings>(HttpMethod.Patch, "/api/match", new { mode = "TwoVsTwo" });
        await _api.SendAsync<Player>(HttpMethod.Patch, "/api/players/1", new { name = "Ana", points = 3 });
        await _api.SendAsync<TeamScore>(HttpMethod.Patch, "/api/teams/b", new { points = 2 });

        var state = await _api.SendAsync<PlayersState>(HttpMethod.Post, "/api/players/reset-scores");

        Assert.Equal("Ana", state.Players[0].Name);
        Assert.Equal(0, state.Players[0].Points);
        Assert.Equal(0, state.Teams[1].Points);
    }

    [Fact]
    public async Task Teams_ChangeAndAdjust()
    {
        await _api.SendAsync<MatchSettings>(HttpMethod.Patch, "/api/match", new { pointsToWin = 8, format = "BestOf3" });

        var set = await _api.SendAsync<TeamScore>(HttpMethod.Patch, "/api/teams/A", new { points = 4, gameWins = 1 }); // Either case
        var adjusted = await _api.SendAsync<TeamScore>(HttpMethod.Post, "/api/teams/a/adjust", new { points = -1, gameWins = 5 });

        Assert.Equal(new TeamScore(4, 1), set);
        Assert.Equal(new TeamScore(3, 2), adjusted); // Game wins stop at 2 in best of 3
        Assert.Equal(adjusted, await _api.GetAsync<TeamScore>("/api/teams/a"));
        Assert.Equal(["At most 8, the match's points to win."], (await _api.InvalidAsync(HttpMethod.Patch, "/api/teams/b", new { points = 9 }))["points"]);
        await _api.SendAsync<MatchSettings>(HttpMethod.Patch, "/api/match", new { format = "BestOf1" });
        Assert.Equal(["At most 1, the most a team can win in this format."], (await _api.InvalidAsync(HttpMethod.Patch, "/api/teams/b", new { gameWins = 2 }))["gameWins"]);
    }

    [Theory]
    [InlineData("GET", "/api/teams/c")]
    [InlineData("PATCH", "/api/teams/c")]
    [InlineData("POST", "/api/teams/c/adjust")]
    public async Task UnknownTeam_Is404(string method, string path)
    {
        var detail = await _api.NotFoundAsync(new HttpMethod(method), path, method == "GET" ? null : new { points = 1 });

        Assert.Equal("No team 'c'. Teams are a and b.", detail);
    }

    private async Task LoadCardsAsync()
    {
        var source = (InMemoryCardSource)factory.Services.GetRequiredService<ICardSource>();
        source.Cards = [Jinx, JinxUnit, Forge];
        await factory.Services.GetRequiredService<CardCatalog>().RefreshAsync(CancellationToken.None);
    }

    internal static Card TestCard(string id, string name, CardType type, CardSupertype? supertype = null) =>
        new(id, $"ogn-{id}", name, null, type, supertype, "Fury", null, "Origins", $"https://cards.test/{id}.png", Landscape: type == CardType.Battlefield);
}
