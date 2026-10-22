using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Riftcaster.Contracts;
using Riftcaster.Core.Match;
using Riftcaster.Core.Players;
using Riftcaster.Core.Settings;
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

        // REST (issue #9): the Match setup page.
        var match = app.MapGroup("/api/match").WithTags("Match");

        match.MapGet("/", (MatchService service) => TypedResults.Ok(service.Settings))
            .WithSummary("Get the match settings");

        match.MapPatch("/", Results<Ok<MatchSettings>, ValidationProblem> (MatchSettingsChange change, MatchService service) =>
            {
                if (change.Validate() is { } problem)
                {
                    return problem;
                }

                service.Update(change.ApplyTo);
                return TypedResults.Ok(service.Settings);
            })
            .WithSummary("Change the match settings")
            .WithDescription("Only the fields sent change. Scores above new limits are lowered to them, and an untouched timer follows a new duration.");

        match.MapPost("/reset-to-defaults", (MatchService service, AppSettingsService settings) =>
            {
                service.ResetToDefaults(settings);
                return TypedResults.Ok(service.Settings);
            })
            .WithSummary("Reset the match settings to the defaults")
            .WithDescription("Gives the match the defaults chosen on the Settings page (the design's until some are chosen). Scores, names and cards are kept; scores above the new limits are lowered to them.");

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
    bool? AllowOvertime = null)
{
    /// <summary>
    /// Makes these changes to a set of match settings.
    /// </summary>
    /// <param name="settings">The settings to change.</param>
    /// <returns>The settings, with the fields that were sent changed.</returns>
    internal MatchSettings ApplyTo(MatchSettings settings) => settings with
    {
        PointsToWin = PointsToWin ?? settings.PointsToWin,
        Format = Format ?? settings.Format,
        Mode = Mode ?? settings.Mode,
        DurationMinutes = DurationMinutes ?? settings.DurationMinutes,
        AllowOvertime = AllowOvertime ?? settings.AllowOvertime,
    };

    /// <summary>
    /// Checks the fields the validation attributes can't: the enums' JSON also accepts numbers, so a
    /// number that isn't one of them gets this far.
    /// </summary>
    /// <param name="prefix">Goes before the field's name in the error, such as "matchDefaults." for a part of the settings.</param>
    /// <returns>A 400 naming the field, or null if the changes are valid.</returns>
    internal ValidationProblem? Validate(string prefix = "")
    {
        if (Format is { } format && !Enum.IsDefined(format))
        {
            return ApiProblems.Invalid(prefix + "format", "Use BestOf1 or BestOf3.");
        }

        if (Mode is { } mode && !Enum.IsDefined(mode))
        {
            return ApiProblems.Invalid(prefix + "mode", "Use OneVsOne, TwoVsTwo, FreeForAll3 or FreeForAll4.");
        }

        return null;
    }
}
