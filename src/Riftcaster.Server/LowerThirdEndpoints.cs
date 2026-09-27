using System.Runtime.CompilerServices;
using Riftcaster.Contracts;
using Riftcaster.Core.LowerThird;

namespace Riftcaster.Server;

public static class LowerThirdEndpoints
{
    public static WebApplication MapLowerThird(this WebApplication app)
    {
        app.MapGet("/api/lower-third/events",
            (LowerThirdService lowerThird, IHostApplicationLifetime lifetime, CancellationToken requestAborted) =>
                TypedResults.ServerSentEvents(WatchUntilShutdown(lowerThird, requestAborted, lifetime.ApplicationStopping)));

        return app;
    }

    private static async IAsyncEnumerable<LowerThirdState> WatchUntilShutdown(
        LowerThirdService lowerThird,
        [EnumeratorCancellation] CancellationToken requestAborted,
        CancellationToken stopping)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(requestAborted, stopping);
        await foreach (var state in lowerThird.WatchAsync(linked.Token))
        {
            yield return state;
        }
    }
}
