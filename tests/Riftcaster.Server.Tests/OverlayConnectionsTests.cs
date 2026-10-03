using System.Net.WebSockets;
using Microsoft.Extensions.DependencyInjection;
using Riftcaster.Contracts;
using Riftcaster.Core.Overlays;

namespace Riftcaster.Server.Tests;

// Its own test server (a fixture per class), so no other test's sockets are counted.
public class OverlayConnectionsTests(RiftcasterWebApplicationFactory factory) : IClassFixture<RiftcasterWebApplicationFactory>
{
    [Fact]
    public async Task Streams_AreCountedWhileOpen()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // starting the test server can be slow on a cold CI runner
        var connections = factory.Services.GetRequiredService<OverlayConnections>();

        using var timer = await factory.ConnectSocketAsync("/api/timer/events", cts.Token);
        await timer.ReceiveStateAsync<TimerState>(cts.Token); // The server has accepted it once it sends
        using var match = await factory.ConnectSocketAsync("/api/match/events", cts.Token);
        await match.ReceiveStateAsync<MatchState>(cts.Token);

        Assert.Equal(2, connections.Count);

        await timer.CloseAsync(WebSocketCloseStatus.NormalClosure, null, cts.Token);
        await WaitUntilAsync(() => connections.Count == 1, cts.Token);

        match.Abort(); // Dropped without a close, as when OBS quits
        await WaitUntilAsync(() => connections.Count == 0, cts.Token);
    }

    // The server uncounts a socket just after its side of the close, so give it a moment.
    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        while (!condition())
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken);
        }
    }
}
