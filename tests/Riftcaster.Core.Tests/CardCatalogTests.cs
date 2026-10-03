using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Riftcaster.Contracts;
using Riftcaster.Core.Cards;

namespace Riftcaster.Core.Tests;

public class CardCatalogTests
{
    private static readonly Card Jinx = NewCard("1", "Jinx, Loose Cannon", CardType.Legend, domain: "Fury / Chaos");
    private static readonly Card JinxMetal = NewCard("2", "Jinx, Loose Cannon", CardType.Legend, domain: "Fury / Chaos", variant: "Metal");
    private static readonly Card Poppy = NewCard("3", "Poppy, Paragon", CardType.Unit, CardSupertype.Champion, domain: "Body");
    private static readonly Card Recruit = NewCard("4", "Recruit", CardType.Unit, domain: "Order");
    private static readonly Card StarSpring = NewCard("5", "Star Spring", CardType.Battlefield, domain: "Colorless");
    private static readonly Card Jinxed = NewCard("6", "Hex Jinxed", CardType.Spell, domain: "Chaos");

    // How long a test waits for a refresh before failing, so a regression fails rather than hangs.
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    private readonly InMemoryCardStore _store = new();

    private readonly FakeCardSource _source = new();

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Constructor_NothingSaved_StartsEmpty()
    {
        var catalog = CreateCatalog();

        Assert.Empty(catalog.Cards);
        Assert.Null(catalog.FetchedAt);
    }

    [Fact]
    public void Constructor_LoadsSavedCards()
    {
        _store.Save(new CardCatalogSnapshot([Jinx, Poppy], _time.GetUtcNow()));

        var catalog = CreateCatalog();

        Assert.Equal([Jinx, Poppy], catalog.Cards);
        Assert.Equal(_time.GetUtcNow(), catalog.FetchedAt);
    }

    [Fact]
    public async Task RefreshAsync_SwapsInSavesAndRaisesChanged()
    {
        var catalog = CreateCatalog();
        var changed = 0;
        catalog.Changed += () => changed++;
        _source.Cards = [Jinx, Poppy];

        var refreshed = await catalog.RefreshAsync(CancellationToken.None);

        Assert.True(refreshed);
        Assert.Equal([Jinx, Poppy], catalog.Cards);
        Assert.Equal([Jinx, Poppy], _store.Snapshot?.Cards);
        Assert.Equal(_time.GetUtcNow(), catalog.FetchedAt);
        Assert.Equal(1, changed);
    }

    [Fact]
    public async Task RefreshAsync_SameCards_SavesTheNewTimeWithoutRaisingChanged()
    {
        _store.Save(new CardCatalogSnapshot([Jinx], _time.GetUtcNow()));
        var catalog = CreateCatalog();
        var changed = 0;
        catalog.Changed += () => changed++;
        _source.Cards = [Jinx];
        _time.Advance(TimeSpan.FromDays(1));

        await catalog.RefreshAsync(CancellationToken.None);

        Assert.Equal(_time.GetUtcNow(), _store.Snapshot?.FetchedAt);
        Assert.Equal(0, changed);
    }

    public static TheoryData<Exception> FetchFailures() =>
    [
        new HttpRequestException("Forbidden"),
        new System.Text.Json.JsonException("Not a card list"),
        new TaskCanceledException("HttpClient timed out"), // What HttpClient throws when its own timeout passes
    ];

    [Theory]
    [MemberData(nameof(FetchFailures))]
    public async Task RefreshAsync_FetchFails_KeepsTheCardsItHas(Exception failure)
    {
        _store.Save(new CardCatalogSnapshot([Jinx], _time.GetUtcNow()));
        var catalog = CreateCatalog();
        _source.Failure = failure;

        var refreshed = await catalog.RefreshAsync(CancellationToken.None);

        Assert.False(refreshed);
        Assert.Equal([Jinx], catalog.Cards);
        Assert.Equal(1, _store.SaveCount); // Only the arranged save
    }

    [Fact]
    public async Task RefreshAsync_NoCards_KeepsTheCardsItHas()
    {
        _store.Save(new CardCatalogSnapshot([Jinx], _time.GetUtcNow()));
        var catalog = CreateCatalog();
        _source.Cards = [];

        var refreshed = await catalog.RefreshAsync(CancellationToken.None);

        Assert.False(refreshed);
        Assert.Equal([Jinx], catalog.Cards);
    }

    [Fact]
    public async Task RefreshAsync_FarFewerCards_KeepsTheCardsItHas()
    {
        var catalog = CreateCatalog([Jinx, Poppy, Recruit]);
        _source.Cards = [Jinx]; // Under half: more likely a broken answer than cards removed

        var refreshed = await catalog.RefreshAsync(CancellationToken.None);

        Assert.False(refreshed);
        Assert.Equal([Jinx, Poppy, Recruit], catalog.Cards);
        Assert.Equal(1, _store.SaveCount); // Only the arranged save
    }

    [Fact]
    public async Task RefreshAsync_SomewhatFewerCards_SwapsThemIn()
    {
        var catalog = CreateCatalog([Jinx, Poppy, Recruit, StarSpring]);
        _source.Cards = [Jinx, Poppy]; // Exactly half: cards can be removed, e.g. stale duplicates

        var refreshed = await catalog.RefreshAsync(CancellationToken.None);

        Assert.True(refreshed);
        Assert.Equal([Jinx, Poppy], catalog.Cards);
    }

    [Fact]
    public async Task RefreshAsync_CancelledDuringFetch_Throws()
    {
        var catalog = CreateCatalog();
        using var cts = new CancellationTokenSource();
        _source.Gate = new TaskCompletionSource(); // Never opened: the fetch waits until it's cancelled
        var refresh = catalog.RefreshAsync(cts.Token);

        cts.Cancel();

        // A cancellation, unlike HttpClient's timeout, isn't a failed fetch to log and survive.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => refresh.WaitAsync(TestTimeout));
    }

    [Fact]
    public async Task RefreshAsync_AlreadyRefreshing_DoesNothing()
    {
        var catalog = CreateCatalog();
        _source.Cards = [Jinx];
        _source.Gate = new TaskCompletionSource();
        var first = catalog.RefreshAsync(CancellationToken.None); // Waits at the gate

        var second = await catalog.RefreshAsync(CancellationToken.None).WaitAsync(TestTimeout);
        _source.Gate.SetResult();

        Assert.False(second);
        Assert.True(await first.WaitAsync(TestTimeout));
        Assert.Equal(1, _source.FetchCount);
    }

    [Fact]
    public void Find_ById()
    {
        var catalog = CreateCatalog([Jinx, Poppy]);

        Assert.Equal(Poppy, catalog.Find("3"));
        Assert.Null(catalog.Find("missing"));
    }

    [Fact]
    public void Lists_HoldTheirCardsByNameThenVariant()
    {
        var catalog = CreateCatalog([StarSpring, Recruit, JinxMetal, Poppy, Jinx, Jinxed]);

        Assert.Equal([Jinx, JinxMetal], catalog.Legends); // Standard printing first
        Assert.Equal([Poppy], catalog.ChampionUnits); // Not Recruit: a unit, but not a champion
        Assert.Equal([StarSpring], catalog.Battlefields);
    }

    [Theory]
    [InlineData("poppy", new[] { "3" })] // Ignores case
    [InlineData("  star  ", new[] { "5" })] // Ignores surrounding spaces
    [InlineData("battlefield", new[] { "5" })] // By type
    [InlineData("chaos", new[] { "6", "1", "2" })] // By domain, each by name: Hex Jinxed, then the Jinxes
    [InlineData("nothing like this", new string[0])]
    public void Search_MatchesNameTypeOrDomain(string query, string[] expectedIds)
    {
        var catalog = CreateCatalog([Jinx, JinxMetal, Poppy, Recruit, StarSpring, Jinxed]);

        var results = catalog.Search(query);

        Assert.Equal(expectedIds, results.Select(card => card.Id));
    }

    [Fact]
    public void Search_NameStartingWithQuery_ComesFirst()
    {
        var catalog = CreateCatalog([Jinxed, Jinx]);

        var results = catalog.Search("jinx");

        Assert.Equal([Jinx, Jinxed], results); // "Jinx, …" starts with it; "Hex Jinxed" only contains it
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Search_EmptyQuery_FindsNothing(string? query)
    {
        var catalog = CreateCatalog([Jinx]);

        Assert.Empty(catalog.Search(query));
    }

    [Fact]
    public void Search_StopsAtTheLimit()
    {
        var catalog = CreateCatalog([Jinx, JinxMetal, Jinxed]);

        Assert.Equal(2, catalog.Search("jinx", limit: 2).Count);
    }

    private CardCatalog CreateCatalog(IReadOnlyList<Card>? saved = null)
    {
        if (saved is not null)
        {
            _store.Save(new CardCatalogSnapshot(saved, _time.GetUtcNow()));
        }

        return new CardCatalog(_store, _source, _time, NullLogger<CardCatalog>.Instance);
    }

    private static Card NewCard(string id, string name, CardType type, CardSupertype? supertype = null, string domain = "Fury", string? variant = null) =>
        new(id, $"tst-{id}", name, variant, type, supertype, domain, Energy: null, "Test Set", $"https://cards.test/{id}.png", Landscape: false);

    /// <summary>
    /// Returns <see cref="Cards"/>, or throws <see cref="Failure"/>. If <see cref="Gate"/> is set, it
    /// first waits for the gate to open, or for the fetch to be cancelled.
    /// </summary>
    private sealed class FakeCardSource : ICardSource
    {
        public IReadOnlyList<Card> Cards { get; set; } = [];

        public Exception? Failure { get; set; }

        public TaskCompletionSource? Gate { get; set; }

        public int FetchCount { get; private set; }

        public async Task<IReadOnlyList<Card>> FetchAllAsync(CancellationToken cancellationToken)
        {
            FetchCount++;
            if (Gate is not null)
            {
                await Gate.Task.WaitAsync(cancellationToken);
            }

            return Failure is null ? Cards : throw Failure;
        }
    }
}
