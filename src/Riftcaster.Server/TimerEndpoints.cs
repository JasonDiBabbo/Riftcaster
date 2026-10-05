using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Riftcaster.Contracts;
using Riftcaster.Core.Timer;

namespace Riftcaster.Server;

public static class TimerEndpoints
{
    public static WebApplication MapTimer(this WebApplication app)
    {
        app.MapGet("/api/timer/events",
            (HttpContext context, TimerService timer, IHostApplicationLifetime lifetime) =>
                StateSockets.SendStatesAsync(context, timer.WatchAsync, lifetime.ApplicationStopping))
            .ExcludeFromDescription(); // A WebSocket, which OpenAPI can't describe

        // REST (issue #9): the same actions as the admin's Timer panel. Each returns the timer as it
        // is afterwards.
        var timer = app.MapGroup("/api/timer").WithTags("Timer");

        timer.MapGet("/", (TimerService service) => TypedResults.Ok(service.State))
            .WithSummary("Get the timer");

        timer.MapPost("/start", (TimerService service) => Act(service, service.Start))
            .WithSummary("Start or resume the timer")
            .WithDescription("Does nothing if it's running, or if it has run out with overtime off.");

        timer.MapPost("/pause", (TimerService service) => Act(service, service.Pause))
            .WithSummary("Pause the timer")
            .WithDescription("Keeps the time run so far. Does nothing if it isn't running.");

        timer.MapPost("/reset", (TimerService service) => Act(service, service.Reset))
            .WithSummary("Reset the timer")
            .WithDescription("Stops it, clears the time run, and sets it back to the match duration.");

        timer.MapPost("/adjust", (TimerAdjustment adjustment, TimerService service) =>
                Act(service, () => service.Adjust(adjustment.Seconds!.Value)))
            .WithSummary("Add or take away time")
            .WithDescription("Changes the timer's length, like the admin dashboard's −1m and +1m buttons. The time run is unchanged, and the length stays between 0 and 999:59.");

        timer.MapPut("/remaining", (TimerRemaining remaining, TimerService service) =>
                Act(service, () => service.SetRemaining(remaining.Seconds!.Value)))
            .WithSummary("Set the time remaining")
            .WithDescription("The timer restarts its count from this time. A running timer keeps running.");

        return app;
    }

    private static Ok<TimerState> Act(TimerService service, Action action)
    {
        action();
        return TypedResults.Ok(service.State);
    }
}

/// <summary>
/// Time to add to the timer, or take away.
/// </summary>
/// <param name="Seconds">Seconds to add, or take away if negative: 60 for +1m, -60 for −1m.</param>
public record TimerAdjustment(
    [property: Required, Range(-TimerService.MaxTotalSeconds, TimerService.MaxTotalSeconds)] int? Seconds);

/// <summary>
/// The time the timer should have left.
/// </summary>
/// <param name="Seconds">The time remaining, from 0 to 59999 (999:59).</param>
public record TimerRemaining(
    [property: Required, Range(0, TimerService.MaxTotalSeconds)] int? Seconds);
