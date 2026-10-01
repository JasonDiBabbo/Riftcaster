using System.Runtime.CompilerServices;

namespace Riftcaster.Server;

/// <summary>
/// Helpers for the server-sent event endpoints that stream a service's state to the overlays.
/// </summary>
internal static class EventStreams
{
    /// <summary>
    /// Runs a service's watch until the client disconnects or the server shuts down, whichever comes first.
    /// </summary>
    /// <remarks>
    /// Without the shutdown token, an overlay left open in OBS would keep the server from stopping,
    /// since its request never ends by itself.
    /// </remarks>
    /// <typeparam name="T">The state the service streams.</typeparam>
    /// <param name="watch">Starts the service's watch, for example <c>players.WatchAsync</c>.</param>
    /// <param name="requestAborted">Cancelled when the client disconnects.</param>
    /// <param name="stopping">Cancelled when the server starts shutting down.</param>
    /// <returns>The service's states, until either token is cancelled.</returns>
    public static async IAsyncEnumerable<T> UntilShutdown<T>(
        Func<CancellationToken, IAsyncEnumerable<T>> watch,
        [EnumeratorCancellation] CancellationToken requestAborted,
        CancellationToken stopping)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(requestAborted, stopping);
        await foreach (var state in watch(linked.Token))
        {
            yield return state;
        }
    }
}
