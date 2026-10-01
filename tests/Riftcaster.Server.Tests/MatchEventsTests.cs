using System.Net.ServerSentEvents;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Riftcaster.Contracts;
using Riftcaster.Core.Players;

namespace Riftcaster.Server.Tests;

public class MatchEventsTests(RiftcasterWebApplicationFactory factory) : IClassFixture<RiftcasterWebApplicationFactory>
{
    [Fact]
    public async Task Events_StreamsCurrentStateThenChanges()
    {
        var client = factory.CreateClient(); // starts the test server, which can be slow on a cold CI runner
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        using var response = await client.GetAsync("/api/match/events", HttpCompletionOption.ResponseHeadersRead, cts.Token);
        response.EnsureSuccessStatusCode();
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        await using var body = await response.Content.ReadAsStreamAsync(cts.Token);
        await using var events = SseParser.Create(body).EnumerateAsync(cts.Token).GetAsyncEnumerator(cts.Token);

        // First event: the current state (the default settings and four empty players)
        Assert.True(await events.MoveNextAsync());
        var initial = JsonSerializer.Deserialize<MatchState>(events.Current.Data, JsonSerializerOptions.Web);
        Assert.NotNull(initial);
        Assert.Equal(PlayersService.MaxPlayers, initial.Players.Players.Count);

        // Change a player through the server's own service
        var players = factory.Services.GetRequiredService<PlayersService>();
        players.UpdatePlayer(0, player => player with { Name = "Mara" });

        // Next event: the change
        Assert.True(await events.MoveNextAsync());
        Assert.Contains("\"mode\":\"OneVsOne\"", events.Current.Data);
        var changed = JsonSerializer.Deserialize<MatchState>(events.Current.Data, JsonSerializerOptions.Web);
        Assert.Equal("Mara", changed?.Players.Players[0].Name);
    }
}
