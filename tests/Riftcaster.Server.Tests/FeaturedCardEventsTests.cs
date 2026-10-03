using Microsoft.Extensions.DependencyInjection;
using Riftcaster.Contracts;
using Riftcaster.Core.Cards;
using Riftcaster.Core.FeaturedCard;

namespace Riftcaster.Server.Tests;

public class FeaturedCardEventsTests(RiftcasterWebApplicationFactory factory) : IClassFixture<RiftcasterWebApplicationFactory>
{
    private static readonly Card Jinx = new("1", "ogn-251-298", "Jinx, Loose Cannon", null, CardType.Legend, null, "Fury / Chaos", null, "Origins", "https://cards.test/1.png", Landscape: false);

    [Fact]
    public async Task Events_SendsCurrentStateThenChanges()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // starting the test server can be slow on a cold CI runner
        using var socket = await factory.ConnectSocketAsync("/api/featured-card/events", cts.Token);

        // First message: the current state (nothing featured)
        var (_, initial) = await socket.ReceiveStateAsync<FeaturedCardState>(cts.Token);
        Assert.Null(initial?.Card);

        // Put a card in the catalogue, then feature it through the server's own service
        await LoadCatalogAsync([Jinx], cts.Token);
        Assert.True(factory.Services.GetRequiredService<FeaturedCardService>().Feature(Jinx.Id));

        // Next message: the change
        var (json, changed) = await socket.ReceiveStateAsync<FeaturedCardState>(cts.Token);
        Assert.Contains("\"type\":\"Legend\"", json);
        Assert.Equal(Jinx, changed?.Card);
    }

    // The test server's catalogue starts empty. The refresher may be fetching at the same moment
    // (a refresh then does nothing), so try until the cards are in.
    private async Task LoadCatalogAsync(IReadOnlyList<Card> cards, CancellationToken cancellationToken)
    {
        ((InMemoryCardSource)factory.Services.GetRequiredService<ICardSource>()).Cards = cards;
        var catalog = factory.Services.GetRequiredService<CardCatalog>();

        while (!await catalog.RefreshAsync(cancellationToken))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken);
        }
    }
}
