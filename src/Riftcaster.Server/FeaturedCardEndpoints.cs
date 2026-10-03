using Riftcaster.Core.FeaturedCard;

namespace Riftcaster.Server;

public static class FeaturedCardEndpoints
{
    public static WebApplication MapFeaturedCard(this WebApplication app)
    {
        app.MapGet("/api/featured-card/events",
            (HttpContext context, FeaturedCardService featuredCard, IHostApplicationLifetime lifetime) =>
                StateSockets.SendStatesAsync(context, featuredCard.WatchAsync, lifetime.ApplicationStopping));

        return app;
    }
}
