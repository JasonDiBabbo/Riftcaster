using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Riftcaster.Contracts;
using Riftcaster.Core.Cards;

namespace Riftcaster.Core.Tests;

public class RiftcodexCardSourceTests
{
    private readonly InstantTimeProvider _time = new();

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
    public async Task FetchAllAsync_Forbidden_FailsWithoutTryingAgain()
    {
        // What Riftcodex answers without a User-Agent: trying again would only be refused again.
        var api = new FakeApi(new HttpResponseMessage(HttpStatusCode.Forbidden));

        await Assert.ThrowsAsync<HttpRequestException>(() => CreateSource(api).FetchAllAsync(CancellationToken.None));
        Assert.Single(api.Requests);
    }

    public static TheoryData<object> TransientFailures() =>
    [
        new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
        new HttpResponseMessage(HttpStatusCode.TooManyRequests),
        new HttpRequestException("Connection refused"), // No answer at all
        new TaskCanceledException("HttpClient timed out"), // HttpClient's own timeout
    ];

    [Theory]
    [MemberData(nameof(TransientFailures))]
    public async Task FetchAllAsync_PageFailsOnce_TriesItAgain(object failure)
    {
        var api = new FakeApi(failure, Page([Legend], pages: 1));

        var cards = await CreateSource(api).FetchAllAsync(CancellationToken.None);

        Assert.Equal(["Vi, Piltover Enforcer"], cards.Select(card => card.Name));
        Assert.Equal(["/cards?page=1&size=100", "/cards?page=1&size=100"], api.Requests);
        Assert.Equal([RiftcodexCardSource.RetryDelay], _time.Delays);
    }

    [Fact]
    public async Task FetchAllAsync_LaterPageFails_TriesOnlyThatPageAgain()
    {
        var api = new FakeApi(Page([Legend], pages: 2), new HttpResponseMessage(HttpStatusCode.BadGateway), Page([Battlefield], pages: 2));

        var cards = await CreateSource(api).FetchAllAsync(CancellationToken.None);

        Assert.Equal(2, cards.Count);
        Assert.Equal(["/cards?page=1&size=100", "/cards?page=2&size=100", "/cards?page=2&size=100"], api.Requests);
    }

    [Fact]
    public async Task FetchAllAsync_PageKeepsFailing_GivesUpAfterMaxAttempts()
    {
        var api = new FakeApi(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        await Assert.ThrowsAsync<HttpRequestException>(() => CreateSource(api).FetchAllAsync(CancellationToken.None));
        Assert.Equal(RiftcodexCardSource.MaxAttempts, api.Requests.Count);
        Assert.Equal([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)], _time.Delays); // Each pause longer
    }

    [Fact]
    public async Task FetchAllAsync_Cancelled_DoesNotTryAgain()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var api = new FakeApi(new TaskCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateSource(api).FetchAllAsync(cts.Token));
        Assert.Empty(_time.Delays);
    }

    private RiftcodexCardSource CreateSource(FakeApi api) =>
        new(new HttpClient(api) { BaseAddress = new Uri("https://api.riftcodex.test/") }, _time, NullLogger<RiftcodexCardSource>.Instance);

    private static HttpResponseMessage Page(string[] items, int pages) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            $$"""{"items":[{{string.Join(",", items)}}],"total":{{items.Length}},"page":1,"size":100,"pages":{{pages}}}""",
            Encoding.UTF8,
            "application/json"),
    };

    /// <summary>
    /// Answers each request with the next canned response (or throws it, for an exception), and
    /// records what was asked for.
    /// </summary>
    private sealed class FakeApi(params object[] responses) : HttpMessageHandler
    {
        private readonly Queue<object> _responses = new(responses);

        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.PathAndQuery);
            return _responses.Dequeue() switch
            {
                HttpResponseMessage response => Task.FromResult(response),
                Exception exception => Task.FromException<HttpResponseMessage>(exception),
                var other => throw new InvalidOperationException($"Not a response: {other}"),
            };
        }
    }

    /// <summary>
    /// A clock whose delays finish at once, and which records how long each was meant to be, so a
    /// test can check the pauses without waiting for them.
    /// </summary>
    private sealed class InstantTimeProvider : TimeProvider
    {
        public List<TimeSpan> Delays { get; } = [];

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Delays.Add(dueTime);
            return base.CreateTimer(callback, state, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        }
    }
}
