using Microsoft.Extensions.DependencyInjection;
using Riftcaster.Contracts;
using Riftcaster.Core.Players;

namespace Riftcaster.Server.Tests;

public class MatchEventsTests(RiftcasterWebApplicationFactory factory) : IClassFixture<RiftcasterWebApplicationFactory>
{
    [Fact]
    public async Task Events_SendsCurrentStateThenChanges()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // starting the test server can be slow on a cold CI runner
        using var socket = await factory.ConnectSocketAsync("/api/match/events", cts.Token);

        // First message: the current state (the default settings and four empty players)
        var (_, initial) = await socket.ReceiveStateAsync<MatchState>(cts.Token);
        Assert.NotNull(initial);
        Assert.Equal(PlayersService.MaxPlayers, initial.Players.Players.Count);

        // Change a player through the server's own service
        var players = factory.Services.GetRequiredService<PlayersService>();
        players.UpdatePlayer(0, player => player with { Name = "Mara" });

        // Next message: the change
        var (json, changed) = await socket.ReceiveStateAsync<MatchState>(cts.Token);
        Assert.Contains("\"mode\":\"OneVsOne\"", json);
        Assert.Equal("Mara", changed?.Players.Players[0].Name);
    }
}
