using Riftcaster.Core.Timer;

namespace Riftcaster.Server;

public static class TimerEndpoints
{
    public static WebApplication MapTimer(this WebApplication app)
    {
        app.MapGet("/api/timer/events",
            (HttpContext context, TimerService timer, IHostApplicationLifetime lifetime) =>
                StateSockets.SendStatesAsync(context, timer.WatchAsync, lifetime.ApplicationStopping));

        return app;
    }
}
