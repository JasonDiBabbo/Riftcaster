using System.Net.ServerSentEvents;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Riftcaster.Contracts;
using Riftcaster.Core.Timer;

namespace Riftcaster.Server.Tests;

public class TimerEventsTests(RiftcasterWebApplicationFactory factory) : IClassFixture<RiftcasterWebApplicationFactory>
{
    [Fact]
    public async Task Events_StreamsCurrentStateThenChanges()
    {
        var client = factory.CreateClient(); // starts the test server, which can be slow on a cold CI runner
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        using var response = await client.GetAsync("/api/timer/events", HttpCompletionOption.ResponseHeadersRead, cts.Token);
        response.EnsureSuccessStatusCode();
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        await using var body = await response.Content.ReadAsStreamAsync(cts.Token);
        await using var events = SseParser.Create(body).EnumerateAsync(cts.Token).GetAsyncEnumerator(cts.Token);

        // First event: the current state (ready at the default 50 minutes)
        Assert.True(await events.MoveNextAsync());
        var initial = JsonSerializer.Deserialize<TimerState>(events.Current.Data, JsonSerializerOptions.Web);
        Assert.Equal(new TimerState(50 * 60, 0, Running: false, AllowOvertime: true), initial);

        // Start the timer through the server's own service
        var timer = factory.Services.GetRequiredService<TimerService>();
        timer.Start();

        // Next event: the change
        Assert.True(await events.MoveNextAsync());
        Assert.Contains("\"running\":true", events.Current.Data);
        var started = JsonSerializer.Deserialize<TimerState>(events.Current.Data, JsonSerializerOptions.Web);
        Assert.True(started?.Running);
    }
}
