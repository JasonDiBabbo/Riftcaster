using Riftcaster.Core.Players;

namespace Riftcaster.Server;

public static class MatchEndpoints
{
    public static WebApplication MapMatch(this WebApplication app)
    {
        app.MapGet("/api/match/events",
            (PlayersService players, IHostApplicationLifetime lifetime, CancellationToken requestAborted) =>
                TypedResults.ServerSentEvents(EventStreams.UntilShutdown(players.WatchAsync, requestAborted, lifetime.ApplicationStopping)));

        return app;
    }
}
