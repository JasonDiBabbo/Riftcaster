using System.Net.ServerSentEvents;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Riftcaster.Contracts;
using Riftcaster.Core.LowerThird;

namespace Riftcaster.Server.Tests;

public class LowerThirdEventsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Events_StreamsCurrentStateThenChanges()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var client = factory.CreateClient();

        // 1. Send the request, but return as soon as the headers arrive
        using var response = await client.GetAsync("/api/lower-third/events", HttpCompletionOption.ResponseHeadersRead, cts.Token);
        response.EnsureSuccessStatusCode();
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        // 2. Turn the raw body into a stream of SSE events
        await using var body = await response.Content.ReadAsStreamAsync(cts.Token);
        await using var events = SseParser.Create(body).EnumerateAsync(cts.Token).GetAsyncEnumerator(cts.Token);

        // 3. First event: the current state (nothing showing)
        Assert.True(await events.MoveNextAsync());
        var initial = JsonSerializer.Deserialize<LowerThirdState>(events.Current.Data, JsonSerializerOptions.Web);
        Assert.Null(initial?.Message);

        // 4. Change the state through the server's own service
        var service = factory.Services.GetRequiredService<LowerThirdService>();
        var message = new LowerThirdKeywordMessage("Burn", "Send cards to the trash.");
        service.Show(message);

        // 5. Next event: the change
        Assert.True(await events.MoveNextAsync());
        Assert.Contains("\"type\":\"keyword\"", events.Current.Data);
        var changed = JsonSerializer.Deserialize<LowerThirdState>(events.Current.Data, JsonSerializerOptions.Web);
        Assert.Equal(message, changed?.Message);
    }
}
