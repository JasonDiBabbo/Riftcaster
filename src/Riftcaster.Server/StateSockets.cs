using System.Net.WebSockets;
using System.Text.Json;

namespace Riftcaster.Server;

/// <summary>
/// Sends a service's states to an overlay over a WebSocket: one JSON text message per state, the
/// current state first. Used by the endpoints the overlays connect to.
/// </summary>
/// <remarks>
/// WebSockets rather than server-sent events: over plain HTTP, a browser opens at most 6
/// connections to one host, and OBS shares one browser across all its sources, so a 7th
/// server-sent events stream would never connect. WebSockets aren't counted against that limit.
/// See issue #32.
/// </remarks>
internal static class StateSockets
{
    // How long to wait for an overlay to answer the server's close before giving up on it.
    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(2);

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Accepts the WebSocket and sends <paramref name="watch"/>'s states down it until the overlay
    /// disconnects or the server shuts down.
    /// </summary>
    /// <typeparam name="T">The state the service streams.</typeparam>
    /// <param name="context">The request, which must be a WebSocket request.</param>
    /// <param name="watch">Starts the service's watch, for example <c>timer.WatchAsync</c>.</param>
    /// <param name="stopping">Cancelled when the server starts shutting down.</param>
    /// <returns>A task that completes when the socket has closed.</returns>
    public static async Task SendStatesAsync<T>(
        HttpContext context,
        Func<CancellationToken, IAsyncEnumerable<T>> watch,
        CancellationToken stopping)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();

        // Ends the watch when the overlay leaves or the server stops, whichever comes first.
        using var done = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, stopping);
        var closedByOverlay = WaitForCloseAsync(socket, done);

        try
        {
            await foreach (var state in watch(done.Token))
            {
                var json = JsonSerializer.SerializeToUtf8Bytes(state, SerializerOptions);
                await socket.SendAsync(json, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
            }
        }
        catch (WebSocketException)
        {
            // The overlay went away mid-send (e.g. OBS closed). Nothing left to send to.
        }

        // The overlay asked to close, or the server is stopping: finish the closing handshake.
        if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            await socket.CloseOutputAsync(
                stopping.IsCancellationRequested ? WebSocketCloseStatus.EndpointUnavailable : WebSocketCloseStatus.NormalClosure,
                stopping.IsCancellationRequested ? "Server shutting down" : null,
                CancellationToken.None);
        }

        // Wait for the overlay's side of the close, but not forever: a frozen overlay mustn't hold
        // up the server's shutdown. Aborting ends the read loop, so it always finishes (and stops
        // using `done`) before this method returns and disposes it.
        if (await Task.WhenAny(closedByOverlay, Task.Delay(CloseTimeout)) != closedByOverlay)
        {
            socket.Abort();
        }

        await closedByOverlay;
    }

    /// <summary>
    /// Reads from the socket until the overlay closes it (or the connection drops), then cancels
    /// <paramref name="done"/>. Overlays never send anything, but reading is how a WebSocket
    /// notices that the other side has closed.
    /// </summary>
    private static async Task WaitForCloseAsync(WebSocket socket, CancellationTokenSource done)
    {
        var buffer = new byte[256];

        try
        {
            // Not done.Token: cancelling a read aborts the socket, which would make a clean close
            // impossible. The read ends by itself when the socket closes or drops.
            while (true)
            {
                var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
        }
        catch (WebSocketException)
        {
            // The connection dropped without a close.
        }
        catch (OperationCanceledException)
        {
            // The socket was aborted.
        }
        finally
        {
            done.Cancel();
        }
    }
}
