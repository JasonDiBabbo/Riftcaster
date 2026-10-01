using Riftcaster.Core.LowerThird;

namespace Riftcaster.Server;

public static class LowerThirdEndpoints
{
    public static WebApplication MapLowerThird(this WebApplication app)
    {
        app.MapGet("/api/lower-third/events",
            (LowerThirdService lowerThird, IHostApplicationLifetime lifetime, CancellationToken requestAborted) =>
                TypedResults.ServerSentEvents(EventStreams.UntilShutdown(lowerThird.WatchAsync, requestAborted, lifetime.ApplicationStopping)));

        return app;
    }
}
