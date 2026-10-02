using Microsoft.Extensions.DependencyInjection;
using Riftcaster.Contracts;
using Riftcaster.Core.LowerThird;

namespace Riftcaster.Server.Tests;

public class LowerThirdEventsTests(RiftcasterWebApplicationFactory factory) : IClassFixture<RiftcasterWebApplicationFactory>
{
    [Fact]
    public async Task Events_SendsCurrentStateThenChanges()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // starting the test server can be slow on a cold CI runner
        using var socket = await factory.ConnectSocketAsync("/api/lower-third/events", cts.Token);

        // First message: the current state (nothing showing)
        var (_, initial) = await socket.ReceiveStateAsync<LowerThirdState>(cts.Token);
        Assert.Null(initial?.Message);

        // Change the state through the server's own service
        var service = factory.Services.GetRequiredService<LowerThirdService>();
        var message = new LowerThirdKeywordMessage("Burn", "Send cards to the trash.");
        service.Show(service.Add(message).Id);

        // Next message: the change
        var (json, changed) = await socket.ReceiveStateAsync<LowerThirdState>(cts.Token);
        Assert.Contains("\"type\":\"keyword\"", json);
        Assert.Equal(message, changed?.Message);
    }
}
