using Riftcaster.Core.Timer;

namespace Riftcaster.Server;

public static class TimerEndpoints
{
    public static WebApplication MapTimer(this WebApplication app)
    {
        app.MapGet("/api/timer/events",
            (TimerService timer, IHostApplicationLifetime lifetime, CancellationToken requestAborted) =>
                TypedResults.ServerSentEvents(EventStreams.UntilShutdown(timer.WatchAsync, requestAborted, lifetime.ApplicationStopping)));

        return app;
    }
}
