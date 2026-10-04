using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Riftcaster.Contracts;
using Riftcaster.Core.Match;
using Riftcaster.Core.Players;
using Riftcaster.Server.Api;

namespace Riftcaster.Server;

public static class MatchEndpoints
{
    public static WebApplication MapMatch(this WebApplication app)
    {
        app.MapGet("/api/match/events",
            (HttpContext context, PlayersService players, IHostApplicationLifetime lifetime) =>
                StateSockets.SendStatesAsync(context, players.WatchAsync, lifetime.ApplicationStopping))
            .ExcludeFromDescription(); // A WebSocket, which OpenAPI can't describe

        // REST (issue #9): the admin's Match panel.
        var match = app.MapGroup("/api/match").WithTags("Match");

        match.MapGet("/", (MatchService service) => TypedResults.Ok(service.Settings))
            .WithSummary("Get the match settings");

        match.MapPatch("/", Results<Ok<MatchSettings>, ValidationProblem> (MatchSettingsChange change, MatchService service) =>
            {
                // The enums' JSON also accepts numbers, so a number that isn't one of them gets this far.
                if (change.Format is { } format && !Enum.IsDefined(format))
                {
                    return ApiProblems.Invalid("format", "Use BestOf1 or BestOf3.");
                }

                if (change.Mode is { } mode && !Enum.IsDefined(mode))
                {
                    return ApiProblems.Invalid("mode", "Use OneVsOne, TwoVsTwo, FreeForAll3 or FreeForAll4.");
                }

                service.Update(settings => settings with
                {
                    PointsToWin = change.PointsToWin ?? settings.PointsToWin,
                    Format = change.Format ?? settings.Format,
                    Mode = change.Mode ?? settings.Mode,
                    DurationMinutes = change.DurationMinutes ?? settings.DurationMinutes,
                    AllowOvertime = change.AllowOvertime ?? settings.AllowOvertime,
                });
                return TypedResults.Ok(service.Settings);
            })
            .WithSummary("Change the match settings")
            .WithDescription("Only the fields sent change. Scores above new limits are lowered to them, and an untouched timer follows a new duration.");

        return app;
    }
}

/// <summary>
/// Changes to the match settings. Leave out a field, or send null, to keep it as it is.
/// </summary>
/// <param name="PointsToWin">Points a player (or team, in 2v2) needs to win a game, from 8 to 15.</param>
/// <param name="Format">Best of 1 or best of 3.</param>
/// <param name="Mode">Which players take part: 1v1, 2v2, or free for all with 3 or 4.</param>
/// <param name="DurationMinutes">The match timer's length, from 1 to 180 minutes.</param>
/// <param name="AllowOvertime">Whether the timer keeps counting up after it reaches zero.</param>
public record MatchSettingsChange(
    [property: Range(MatchService.MinPointsToWin, MatchService.MaxPointsToWin)] int? PointsToWin = null,
    MatchFormat? Format = null,
    MatchMode? Mode = null,
    [property: Range(MatchService.MinDurationMinutes, MatchService.MaxDurationMinutes)] int? DurationMinutes = null,
    bool? AllowOvertime = null);
