using Microsoft.Extensions.DependencyInjection;
using Riftcaster.Contracts;
using Riftcaster.Core.Cards;

namespace Riftcaster.Server.Tests;

// Its own test server (a fixture per class): these tests load cards and feature them.
public class FeaturedCardApiTests(RiftcasterWebApplicationFactory factory) : IClassFixture<RiftcasterWebApplicationFactory>
{
    private static readonly Card Jinx = MatchAndPlayersApiTests.TestCard("legend-jinx", "Jinx, Loose Cannon", CardType.Legend);

    private static readonly Card Poppy = MatchAndPlayersApiTests.TestCard("unit-poppy", "Poppy, Paragon", CardType.Unit, CardSupertype.Champion);

    private readonly RestApiClient _api = new(factory.CreateClient());

    [Fact]
    public async Task FeatureAndClear()
    {
        await LoadCardsAsync();

        var featured = await _api.SendAsync<FeaturedCardState>(HttpMethod.Put, "/api/featured-card", new { cardId = Poppy.Id });
        Assert.Equal(Poppy, featured.Card);
        Assert.Equal(Poppy, (await _api.GetAsync<FeaturedCardState>("/api/featured-card")).Card);

        var cleared = await _api.SendAsync<FeaturedCardState>(HttpMethod.Delete, "/api/featured-card");
        Assert.Null(cleared.Card);
    }

    [Fact]
    public async Task Feature_UnknownOrMissingId_Is400()
    {
        await LoadCardsAsync();

        Assert.Contains("No card with id 'nope'", (await _api.InvalidAsync(HttpMethod.Put, "/api/featured-card", new { cardId = "nope" }))["cardId"][0]);
        Assert.Contains("cardId", (await _api.InvalidAsync(HttpMethod.Put, "/api/featured-card", "{}")).Keys);
    }

    [Fact]
    public async Task Search_FindsCards()
    {
        await LoadCardsAsync();

        var found = await _api.GetAsync<List<Card>>("/api/cards?search=jinx");
        var limited = await _api.GetAsync<List<Card>>("/api/cards?search=fury&limit=1"); // The domain matches both

        Assert.Equal([Jinx], found);
        Assert.Single(limited);
        Assert.Equal(Jinx, await _api.GetAsync<Card>($"/api/cards/{Jinx.Id}"));
        Assert.Equal("No card with id 'nope'.", await _api.NotFoundAsync(HttpMethod.Get, "/api/cards/nope"));
    }

    [Theory]
    [InlineData("/api/cards")]
    [InlineData("/api/cards?search=jinx&limit=0")]
    [InlineData("/api/cards?search=jinx&limit=101")]
    public async Task Search_InvalidQuery_Is400(string path)
    {
        var response = await _api.SendRawAsync(HttpMethod.Get, path);

        Assert.Equal(400, (int)response.StatusCode);
    }

    private async Task LoadCardsAsync()
    {
        var source = (InMemoryCardSource)factory.Services.GetRequiredService<ICardSource>();
        source.Cards = [Jinx, Poppy];
        await factory.Services.GetRequiredService<CardCatalog>().RefreshAsync(CancellationToken.None);
    }
}

// A server whose card catalogue never loads, as while the first fetch is running.
public class CardsNotLoadedApiTests(RiftcasterWebApplicationFactory factory) : IClassFixture<RiftcasterWebApplicationFactory>
{
    private readonly RestApiClient _api = new(factory.CreateClient());

    [Fact]
    public async Task ChoosingACard_SaysTheCatalogueIsLoading()
    {
        const string Loading = "The card catalogue hasn't loaded yet. Try again in a minute.";

        Assert.Equal([Loading], (await _api.InvalidAsync(HttpMethod.Put, "/api/featured-card", new { cardId = "any" }))["cardId"]);
        Assert.Equal([Loading], (await _api.InvalidAsync(HttpMethod.Patch, "/api/players/1", new { battlefieldId = "any" }))["battlefieldId"]);
    }
}
