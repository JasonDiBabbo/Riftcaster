using Riftcaster.Core.LowerThird;

namespace Riftcaster.Server;

public static class LowerThirdEndpoints
{
    public static WebApplication MapLowerThird(this WebApplication app)
    {
        app.MapGet("/api/lower-third/events",
            (HttpContext context, LowerThirdService lowerThird, IHostApplicationLifetime lifetime) =>
                StateSockets.SendStatesAsync(context, lowerThird.WatchAsync, lifetime.ApplicationStopping));

        return app;
    }
}
