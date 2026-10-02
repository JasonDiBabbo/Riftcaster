using Riftcaster.Core.Players;

namespace Riftcaster.Server;

public static class MatchEndpoints
{
    public static WebApplication MapMatch(this WebApplication app)
    {
        app.MapGet("/api/match/events",
            (HttpContext context, PlayersService players, IHostApplicationLifetime lifetime) =>
                StateSockets.SendStatesAsync(context, players.WatchAsync, lifetime.ApplicationStopping));

        return app;
    }
}
