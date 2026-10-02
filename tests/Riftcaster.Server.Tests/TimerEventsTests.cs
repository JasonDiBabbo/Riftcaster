using System.Net;
using System.Net.WebSockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Riftcaster.Contracts;
using Riftcaster.Core.Timer;

namespace Riftcaster.Server.Tests;

public class TimerEventsTests(RiftcasterWebApplicationFactory factory) : IClassFixture<RiftcasterWebApplicationFactory>
{
    [Fact]
    public async Task Events_SendsCurrentStateThenChanges()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // starting the test server can be slow on a cold CI runner
        using var socket = await factory.ConnectSocketAsync("/api/timer/events", cts.Token);

        // First message: the current state (ready at the default 50 minutes)
        var (_, initial) = await socket.ReceiveStateAsync<TimerState>(cts.Token);
        Assert.Equal(new TimerState(50 * 60, 0, Running: false, AllowOvertime: true), initial);

        // Start the timer through the server's own service
        var timer = factory.Services.GetRequiredService<TimerService>();
        timer.Start();

        // Next message: the change
        var (json, started) = await socket.ReceiveStateAsync<TimerState>(cts.Token);
        Assert.Contains("\"running\":true", json);
        Assert.True(started?.Running);

        timer.Reset(); // Leave the shared test server's timer as it was
    }

    [Fact]
    public async Task Events_OverlayCloses_ServerClosesToo()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var socket = await factory.ConnectSocketAsync("/api/timer/events", cts.Token);
        await socket.ReceiveStateAsync<TimerState>(cts.Token);

        // CloseAsync completes when the server answers with its own close
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, cts.Token);

        Assert.Equal(WebSocketState.Closed, socket.State);
    }

    [Fact]
    public async Task Events_ServerStopping_ClosesTheSocket()
    {
        // Its own server, since this test stops it.
        await using var stoppingFactory = new RiftcasterWebApplicationFactory();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var socket = await stoppingFactory.ConnectSocketAsync("/api/timer/events", cts.Token);
        await socket.ReceiveStateAsync<TimerState>(cts.Token);

        stoppingFactory.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
        var result = await socket.ReceiveAsync(new byte[256], cts.Token);

        Assert.Equal(WebSocketMessageType.Close, result.MessageType);
        Assert.Equal(WebSocketCloseStatus.EndpointUnavailable, result.CloseStatus);
    }

    [Fact]
    public async Task Events_PlainRequest_IsBadRequest()
    {
        var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/timer/events");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
