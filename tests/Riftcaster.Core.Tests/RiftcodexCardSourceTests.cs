using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Riftcaster.Contracts;
using Riftcaster.Core.Cards;

namespace Riftcaster.Core.Tests;

public class RiftcodexCardSourceTests
{
    // Real cards from the API (trimmed to the fields the source reads, plus one it doesn't), one of
    // each shape the mapping handles: two domains and no cost, a cost, and a landscape image. Their
    // names are normalized by RiftcodexNames (see RiftcodexNamesTests).
    private const string Legend = """
        {"id":"69c4407c9288b1e85d94de8a","riftbound_id":"unl-229*-219","name":"Vi - Piltover Enforcer (Signature)","classification":{"type":"Legend","supertype":null,"rarity":"Rare","domain":["Fury","Order"]},"attributes":{"energy":null,"might":null,"power":null},"set":{"set_id":"UNL","label":"Unleashed"},"media":{"image_url":"https://cmsassets.rgpub.io/vi.png"},"orientation":"portrait"}
        """;

    private const string ChampionUnit = """
        {"id":"69c4407d9288b1e85d94de94","riftbound_id":"unl-116a-219","name":"Poppy - Paragon (Alternate Art)","classification":{"type":"Unit","supertype":"Champion","rarity":"Rare","domain":["Body"]},"attributes":{"energy":5,"might":5,"power":null},"set":{"set_id":"UNL","label":"Unleashed"},"media":{"image_url":"https://cmsassets.rgpub.io/poppy.png"},"orientation":"portrait"}
        """;

    private const string Battlefield = """
        {"id":"69c4407d9288b1e85d94de9e","riftbound_id":"unl-215-219","name":"Star Spring","classification":{"type":"Battlefield","supertype":null,"rarity":"Uncommon","domain":["Colorless"]},"attributes":{"energy":null,"might":null,"power":null},"set":{"set_id":"UNL","label":"Unleashed"},"media":{"image_url":"https://cmsassets.rgpub.io/star-spring.png"},"orientation":"landscape"}
        """;

    [Fact]
    public async Task FetchAllAsync_MapsEachCard()
    {
        var api = new FakeApi(Page([Legend, ChampionUnit, Battlefield], pages: 1));

        var cards = await CreateSource(api).FetchAllAsync(CancellationToken.None);

        Assert.Equal(
            [
                new Card("69c4407c9288b1e85d94de8a", "unl-229*-219", "Vi, Piltover Enforcer", "Signature", CardType.Legend, null, "Fury / Order", null, "Unleashed", "https://cmsassets.rgpub.io/vi.png", Landscape: false),
                new Card("69c4407d9288b1e85d94de94", "unl-116a-219", "Poppy, Paragon", "Alternate Art", CardType.Unit, CardSupertype.Champion, "Body", 5, "Unleashed", "https://cmsassets.rgpub.io/poppy.png", Landscape: false),
                new Card("69c4407d9288b1e85d94de9e", "unl-215-219", "Star Spring", null, CardType.Battlefield, null, "Colorless", null, "Unleashed", "https://cmsassets.rgpub.io/star-spring.png", Landscape: true),
            ],
            cards);
    }

    [Fact]
    public async Task FetchAllAsync_ReadsEveryPage()
    {
        var api = new FakeApi(Page([Legend], pages: 2), Page([Battlefield], pages: 2));

        var cards = await CreateSource(api).FetchAllAsync(CancellationToken.None);

        Assert.Equal(["Vi, Piltover Enforcer", "Star Spring"], cards.Select(card => card.Name));
        Assert.Equal(["/cards?page=1&size=100", "/cards?page=2&size=100"], api.Requests);
    }

    [Fact]
    public async Task FetchAllAsync_SkipsCardsMissingSomething()
    {
        const string NoName = """{"id":"x","riftbound_id":"ogn-1-298","classification":{"type":"Unit"},"set":{"label":"Origins"},"media":{"image_url":"https://x.png"}}""";
        const string NoImage = """{"id":"y","riftbound_id":"ogn-2-298","name":"Y","classification":{"type":"Unit"},"set":{"label":"Origins"},"media":{}}""";
        const string NoCode = """{"id":"z","name":"Z","classification":{"type":"Unit"},"set":{"label":"Origins"},"media":{"image_url":"https://z.png"}}""";
        var api = new FakeApi(Page([NoName, Legend, NoImage, NoCode], pages: 1));

        var cards = await CreateSource(api).FetchAllAsync(CancellationToken.None);

        Assert.Equal(["Vi, Piltover Enforcer"], cards.Select(card => card.Name));
    }

    [Fact]
    public async Task FetchAllAsync_UnknownTypeOrSupertype_KeepsCardAsOther()
    {
        const string NewKind = """{"id":"n","riftbound_id":"new-1-100","name":"Something New","classification":{"type":"Sigil","supertype":"Mythic","domain":["Mind"]},"set":{"label":"Future"},"media":{"image_url":"https://n.png"}}""";
        var api = new FakeApi(Page([NewKind], pages: 1));

        var cards = await CreateSource(api).FetchAllAsync(CancellationToken.None);

        Assert.Equal((CardType.Other, CardSupertype.Other), (cards[0].Type, cards[0].Supertype));
    }

    [Fact]
    public async Task FetchAllAsync_ErrorResponse_Throws()
    {
        var api = new FakeApi(new HttpResponseMessage(HttpStatusCode.Forbidden));

        await Assert.ThrowsAsync<HttpRequestException>(() => CreateSource(api).FetchAllAsync(CancellationToken.None));
    }

    private static RiftcodexCardSource CreateSource(FakeApi api) =>
        new(new HttpClient(api) { BaseAddress = new Uri("https://api.riftcodex.test/") }, NullLogger<RiftcodexCardSource>.Instance);

    private static HttpResponseMessage Page(string[] items, int pages) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            $$"""{"items":[{{string.Join(",", items)}}],"total":{{items.Length}},"page":1,"size":100,"pages":{{pages}}}""",
            Encoding.UTF8,
            "application/json"),
    };

    /// <summary>
    /// Answers each request with the next canned response, and records what was asked for.
    /// </summary>
    private sealed class FakeApi(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.PathAndQuery);
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
