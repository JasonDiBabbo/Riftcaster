using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Riftcaster.Contracts;
using Riftcaster.Core.Cards;
using Riftcaster.Core.Match;
using Riftcaster.Core.Players;
using Riftcaster.Server.Api;

namespace Riftcaster.Server;

/// <summary>
/// REST (issue #9) for the admin's Players panel: the players' details and scores, and the teams'
/// scores in 2v2. Their overlays' stream is /api/match/events (see <see cref="MatchEndpoints"/>).
/// </summary>
/// <remarks>
/// Seats are numbered from 1, as the admin labels them (Player 1 to Player 4), and teams are a and
/// b (Team A and Team B), unlike the 0-based lists in the JSON.
/// </remarks>
public static class PlayersEndpoints
{
    public static WebApplication MapPlayers(this WebApplication app)
    {
        var players = app.MapGroup("/api/players").WithTags("Players");

        players.MapGet("/", (PlayersService service) => TypedResults.Ok(service.State))
            .WithSummary("Get every player and team")
            .WithDescription("All four players and both teams are always there. The match mode decides which are in use: the first two, three or four players, and the teams only in 2v2.");

        players.MapGet("/{seat:int}", Results<Ok<Player>, NotFound<ProblemDetails>> (int seat, PlayersService service) =>
                IsSeat(seat) ? TypedResults.Ok(service.State.Players[seat - 1]) : NoSeat(seat))
            .WithSummary("Get one player")
            .WithDescription("Seats are 1 to 4.");

        players.MapPatch("/{seat:int}", Results<Ok<Player>, NotFound<ProblemDetails>, ValidationProblem> (
                int seat, PlayerChange change, PlayersService service, MatchService match, CardCatalog catalog) =>
            {
                if (!IsSeat(seat))
                {
                    return NoSeat(seat);
                }

                var limits = match.Settings;
                var errors = new Dictionary<string, string[]>();
                CheckAtMost(errors, "points", change.Points, limits.PointsToWin, "the match's points to win");
                CheckAtMost(errors, "gameWins", change.GameWins, limits.MaxGameWins, "the most a player can win in this format");
                var legend = FindCard(errors, "legendId", change.LegendId, catalog, card => card.Type == CardType.Legend, "legend");
                var champion = FindCard(errors, "championId", change.ChampionId, catalog,
                    card => card.Type == CardType.Unit && card.Supertype == CardSupertype.Champion, "champion unit");
                var battlefield = FindCard(errors, "battlefieldId", change.BattlefieldId, catalog, card => card.Type == CardType.Battlefield, "battlefield");
                if (errors.Count > 0)
                {
                    return ApiProblems.Invalid(errors);
                }

                service.UpdatePlayer(seat - 1, player => player with
                {
                    Name = change.Name ?? player.Name,
                    Legend = change.LegendId is null ? player.Legend : legend,
                    Champion = change.ChampionId is null ? player.Champion : champion,
                    Battlefield = change.BattlefieldId is null ? player.Battlefield : battlefield,
                    Points = change.Points ?? player.Points,
                    GameWins = change.GameWins ?? player.GameWins,
                    Xp = change.Xp ?? player.Xp,
                });
                return TypedResults.Ok(service.State.Players[seat - 1]);
            })
            .WithSummary("Change a player")
            .WithDescription("Only the fields sent change. Cards are chosen by id (see GET /api/cards); send an empty id to clear one. Scores must be within the match's limits.");

        players.MapPost("/{seat:int}/adjust", Results<Ok<Player>, NotFound<ProblemDetails>> (
                int seat, PlayerAdjustment adjustment, PlayersService service) =>
            {
                if (!IsSeat(seat))
                {
                    return NoSeat(seat);
                }

                service.UpdatePlayer(seat - 1, player => player with
                {
                    Points = player.Points + (adjustment.Points ?? 0),
                    GameWins = player.GameWins + (adjustment.GameWins ?? 0),
                    Xp = player.Xp + (adjustment.Xp ?? 0),
                });
                return TypedResults.Ok(service.State.Players[seat - 1]);
            })
            .WithSummary("Add to or take from a player's scores")
            .WithDescription("For buttons such as +1 point: { \"points\": 1 }. Scores stop at their limits rather than failing, so pressing +1 at the points to win does nothing.");

        players.MapPost("/swap", (SeatSwap swap, PlayersService service) =>
            {
                service.SwapPlayers(swap.First!.Value - 1, swap.Second!.Value - 1);
                return TypedResults.Ok(service.State);
            })
            .WithSummary("Swap two players' seats")
            .WithDescription("Each player's details and scores move with them. The seat decides the side in 1v1 and the team in 2v2; team scores stay with their team.");

        players.MapPost("/reset-scores", (PlayersService service) =>
            {
                service.ResetScores();
                return TypedResults.Ok(service.State);
            })
            .WithSummary("Reset every score")
            .WithDescription("Sets every player's points, game wins and XP, and every team's scores, to 0. Names and cards are kept.");

        var teams = app.MapGroup("/api/teams").WithTags("Players");

        teams.MapGet("/{team}", Results<Ok<TeamScore>, NotFound<ProblemDetails>> (string team, PlayersService service) =>
                TeamIndex(team) is { } index ? TypedResults.Ok(service.State.Teams[index]) : NoTeam(team))
            .WithSummary("Get a team's score")
            .WithDescription("Teams are a and b. Used in 2v2, where points and game wins belong to the team.");

        teams.MapPatch("/{team}", Results<Ok<TeamScore>, NotFound<ProblemDetails>, ValidationProblem> (
                string team, TeamChange change, PlayersService service, MatchService match) =>
            {
                if (TeamIndex(team) is not { } index)
                {
                    return NoTeam(team);
                }

                var limits = match.Settings;
                var errors = new Dictionary<string, string[]>();
                CheckAtMost(errors, "points", change.Points, limits.PointsToWin, "the match's points to win");
                CheckAtMost(errors, "gameWins", change.GameWins, limits.MaxGameWins, "the most a team can win in this format");
                if (errors.Count > 0)
                {
                    return ApiProblems.Invalid(errors);
                }

                service.UpdateTeam(index, score => score with
                {
                    Points = change.Points ?? score.Points,
                    GameWins = change.GameWins ?? score.GameWins,
                });
                return TypedResults.Ok(service.State.Teams[index]);
            })
            .WithSummary("Change a team's score")
            .WithDescription("Only the fields sent change. Scores must be within the match's limits.");

        teams.MapPost("/{team}/adjust", Results<Ok<TeamScore>, NotFound<ProblemDetails>> (
                string team, TeamAdjustment adjustment, PlayersService service) =>
            {
                if (TeamIndex(team) is not { } index)
                {
                    return NoTeam(team);
                }

                service.UpdateTeam(index, score => score with
                {
                    Points = score.Points + (adjustment.Points ?? 0),
                    GameWins = score.GameWins + (adjustment.GameWins ?? 0),
                });
                return TypedResults.Ok(service.State.Teams[index]);
            })
            .WithSummary("Add to or take from a team's score")
            .WithDescription("Scores stop at their limits rather than failing.");

        return app;
    }

    private static bool IsSeat(int seat) => seat is >= 1 and <= PlayersService.MaxPlayers;

    private static NotFound<ProblemDetails> NoSeat(int seat) =>
        ApiProblems.NotFound($"No player in seat {seat}. Seats are 1 to {PlayersService.MaxPlayers}.");

    private static int? TeamIndex(string team) => team.ToLowerInvariant() switch
    {
        "a" => 0,
        "b" => 1,
        _ => null,
    };

    private static NotFound<ProblemDetails> NoTeam(string team) =>
        ApiProblems.NotFound($"No team '{team}'. Teams are a and b.");

    // Scores' fixed limits are on the requests; these depend on the match settings.
    private static void CheckAtMost(Dictionary<string, string[]> errors, string field, int? value, int max, string what)
    {
        if (value > max)
        {
            errors[field] = [$"At most {max}, {what}."];
        }
    }

    // The card for an id: null for an empty id (clear the card), or when the id is invalid, which
    // adds an error. Not needed when the id isn't sent.
    private static Card? FindCard(Dictionary<string, string[]> errors, string field, string? id, CardCatalog catalog, Func<Card, bool> isKind, string kind)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        if (catalog.Find(id) is not { } card)
        {
            errors[field] = [catalog.Cards.Count == 0
                ? "The card catalogue hasn't loaded yet. Try again in a minute."
                : $"No card with id '{id}'. Find ids with GET /api/cards?search=..."];
            return null;
        }

        if (!isKind(card))
        {
            errors[field] = [$"{card.Name} isn't a {kind}."];
            return null;
        }

        return card;
    }
}

/// <summary>
/// Changes to a player. Leave out a field, or send null, to keep it as it is.
/// </summary>
/// <param name="Name">The player's display name.</param>
/// <param name="LegendId">The id of the player's legend, or an empty string for none.</param>
/// <param name="ChampionId">The id of the player's chosen champion unit, or an empty string for none.</param>
/// <param name="BattlefieldId">The id of the player's battlefield, or an empty string for none.</param>
/// <param name="Points">Points in the current game, from 0 to the match's points to win.</param>
/// <param name="GameWins">Games won in the match: up to 1 in best of 1, 2 in best of 3.</param>
/// <param name="Xp">The player's XP, from 0 to 99.</param>
public record PlayerChange(
    [property: MaxLength(100)] string? Name = null,
    string? LegendId = null,
    string? ChampionId = null,
    string? BattlefieldId = null,
    [property: Range(0, MatchService.MaxPointsToWin)] int? Points = null,
    [property: Range(0, 2)] int? GameWins = null,
    [property: Range(0, PlayersService.MaxXp)] int? Xp = null);

/// <summary>
/// Amounts to add to a player's scores, or take away if negative. Leave out the ones that don't change.
/// </summary>
/// <param name="Points">Points to add, for example 1, or -1 to take one away.</param>
/// <param name="GameWins">Game wins to add.</param>
/// <param name="Xp">XP to add.</param>
public record PlayerAdjustment(
    [property: Range(-100, 100)] int? Points = null,
    [property: Range(-100, 100)] int? GameWins = null,
    [property: Range(-100, 100)] int? Xp = null);

/// <summary>
/// Two seats whose players change places.
/// </summary>
/// <param name="First">One seat, from 1 to 4.</param>
/// <param name="Second">The other seat, from 1 to 4.</param>
public record SeatSwap(
    [property: Required, Range(1, PlayersService.MaxPlayers)] int? First,
    [property: Required, Range(1, PlayersService.MaxPlayers)] int? Second);

/// <summary>
/// Changes to a team's score. Leave out a field, or send null, to keep it as it is.
/// </summary>
/// <param name="Points">Points in the current game, from 0 to the match's points to win.</param>
/// <param name="GameWins">Games won in the match: up to 1 in best of 1, 2 in best of 3.</param>
public record TeamChange(
    [property: Range(0, MatchService.MaxPointsToWin)] int? Points = null,
    [property: Range(0, 2)] int? GameWins = null);

/// <summary>
/// Amounts to add to a team's score, or take away if negative. Leave out the ones that don't change.
/// </summary>
/// <param name="Points">Points to add, for example 1, or -1 to take one away.</param>
/// <param name="GameWins">Game wins to add.</param>
public record TeamAdjustment(
    [property: Range(-100, 100)] int? Points = null,
    [property: Range(-100, 100)] int? GameWins = null);
