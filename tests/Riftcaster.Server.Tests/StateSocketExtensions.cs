using System.Net.WebSockets;
using System.Text.Json;

namespace Riftcaster.Server.Tests;

/// <summary>
/// Connects to the overlays' WebSocket endpoints and reads their states, for the endpoint tests.
/// </summary>
internal static class StateSocketExtensions
{
    /// <summary>
    /// Opens a WebSocket to one of the test server's endpoints, as an overlay would.
    /// </summary>
    /// <param name="factory">The test server.</param>
    /// <param name="path">The endpoint, e.g. "/api/timer/events".</param>
    /// <param name="cancellationToken">Cancels the connection attempt.</param>
    /// <returns>The open socket.</returns>
    public static Task<WebSocket> ConnectSocketAsync(
        this RiftcasterWebApplicationFactory factory,
        string path,
        CancellationToken cancellationToken)
    {
        var uri = new UriBuilder(factory.Server.BaseAddress) { Scheme = "ws", Path = path }.Uri;
        return factory.Server.CreateWebSocketClient().ConnectAsync(uri, cancellationToken);
    }

    /// <summary>
    /// Reads the next message, which may arrive in several frames, and parses it as JSON.
    /// </summary>
    /// <typeparam name="T">The state the endpoint sends.</typeparam>
    /// <param name="socket">The open socket.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The message's text and the state parsed from it.</returns>
    public static async Task<(string Json, T? State)> ReceiveStateAsync<T>(
        this WebSocket socket,
        CancellationToken cancellationToken)
    {
        using var message = new MemoryStream();
        var buffer = new byte[4096];
        WebSocketReceiveResult result;

        do
        {
            result = await socket.ReceiveAsync(buffer, cancellationToken);
            message.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        Assert.Equal(WebSocketMessageType.Text, result.MessageType);
        var json = System.Text.Encoding.UTF8.GetString(message.ToArray());
        return (json, JsonSerializer.Deserialize<T>(json, JsonSerializerOptions.Web));
    }
}
