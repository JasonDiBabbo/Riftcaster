using Microsoft.Extensions.Logging.Abstractions;
using Riftcaster.Contracts;
using Riftcaster.Core.Cards;
using Riftcaster.Core.FeaturedCard;

namespace Riftcaster.Core.Tests;

public class FeaturedCardServiceTests
{
    private static readonly Card Jinx = NewCard("1", "Jinx, Loose Cannon");
    private static readonly Card JinxMetal = NewCard("2", "Jinx, Loose Cannon", variant: "Metal"); // Same code as Jinx
    private static readonly Card Poppy = NewCard("3", "Poppy, Paragon");

    private readonly InMemoryFeaturedCardStore _store = new();

    private readonly InMemoryCardStore _cardStore = new();

    private readonly FeaturedCardService _service;

    private int _changedCount;

    public FeaturedCardServiceTests()
    {
        _cardStore.Save(new CardCatalogSnapshot([Jinx, JinxMetal, Poppy], DateTimeOffset.UnixEpoch));
        _service = CreateService();
        _service.Changed += () => _changedCount++;
    }

    [Fact]
    public void Current_IsNullInitially()
    {
        Assert.Null(_service.Current);
    }

    [Fact]
    public void Constructor_LoadsTheSavedCard()
    {
        _store.Save(new FeaturedCardState(Poppy));

        Assert.Equal(Poppy, CreateService().Current);
    }

    [Fact]
    public void Constructor_SavedCardNotInTheCatalogue_IsStillFeatured()
    {
        // E.g. after a restart, before the first fetch: the saved copy is enough to show it.
        _store.Save(new FeaturedCardState(Poppy));
        var service = new FeaturedCardService(_store, CreateCatalog(new InMemoryCardStore()));

        Assert.Equal(Poppy, service.Current);
    }

    [Fact]
    public void Feature_SetsCurrentSavesAndRaisesChanged()
    {
        var featured = _service.Feature(Jinx.Id);

        Assert.True(featured);
        Assert.Equal(Jinx, _service.Current);
        Assert.Equal(new FeaturedCardState(Jinx), _store.State);
        Assert.Equal(1, _changedCount);
    }

    [Fact]
    public void Feature_TellsPrintingsWithTheSameCodeApart()
    {
        _service.Feature(Jinx.Id);
        _service.Feature(JinxMetal.Id);

        Assert.Equal(JinxMetal, _service.Current);
        Assert.Equal(2, _changedCount);
    }

    [Fact]
    public void Feature_SameCardAgain_DoesNothing()
    {
        _service.Feature(Jinx.Id);
        var saveCount = _store.SaveCount;

        var featured = _service.Feature(Jinx.Id);

        Assert.True(featured);
        Assert.Equal(saveCount, _store.SaveCount);
        Assert.Equal(1, _changedCount);
    }

    [Fact]
    public void Feature_UnknownId_ReturnsFalseAndKeepsTheCurrentCard()
    {
        _service.Feature(Jinx.Id);

        var featured = _service.Feature("missing");

        Assert.False(featured);
        Assert.Equal(Jinx, _service.Current);
        Assert.Equal(1, _changedCount);
    }

    [Fact]
    public void Clear_ClearsSavesAndRaisesChanged()
    {
        _service.Feature(Jinx.Id);

        _service.Clear();

        Assert.Null(_service.Current);
        Assert.Equal(new FeaturedCardState(Card: null), _store.State);
        Assert.Equal(2, _changedCount);
    }

    [Fact]
    public void Clear_WhenNothingFeatured_DoesNothing()
    {
        _service.Clear();

        Assert.Equal(0, _store.SaveCount);
        Assert.Equal(0, _changedCount);
    }

    [Fact]
    public async Task WatchAsync_YieldsCurrentStateFirst()
    {
        _service.Feature(Poppy.Id);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var watch = _service.WatchAsync(cts.Token).GetAsyncEnumerator();

        Assert.True(await watch.MoveNextAsync());
        Assert.Equal(Poppy, watch.Current.Card);
    }

    [Fact]
    public async Task WatchAsync_YieldsChanges()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var watch = _service.WatchAsync(cts.Token).GetAsyncEnumerator();
        Assert.True(await watch.MoveNextAsync()); // Consume the initial state

        _service.Feature(Jinx.Id);

        Assert.True(await watch.MoveNextAsync());
        Assert.Equal(Jinx, watch.Current.Card);
    }

    [Fact]
    public async Task WatchAsync_WhenChangesPileUp_YieldsOnlyLatest()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var watch = _service.WatchAsync(cts.Token).GetAsyncEnumerator();
        Assert.True(await watch.MoveNextAsync()); // Consume the initial state

        _service.Feature(Jinx.Id);
        _service.Feature(Poppy.Id);

        Assert.True(await watch.MoveNextAsync()); // Only the latest change
        Assert.Equal(Poppy, watch.Current.Card);
    }

    [Fact]
    public async Task WatchAsync_EndsWhenCancelled()
    {
        using var cts = new CancellationTokenSource(); // no timeout: we cancel it ourselves
        await using var watch = _service.WatchAsync(cts.Token).GetAsyncEnumerator();
        Assert.True(await watch.MoveNextAsync());

        cts.Cancel();

        Assert.False(await watch.MoveNextAsync()); // Stream ended normally
    }

    private FeaturedCardService CreateService() => new(_store, CreateCatalog(_cardStore));

    private static CardCatalog CreateCatalog(InMemoryCardStore cards) =>
        new(cards, new InMemoryCardSource(), TimeProvider.System, NullLogger<CardCatalog>.Instance);

    private static Card NewCard(string id, string name, string? variant = null) =>
        new(id, "tst-1", name, variant, CardType.Legend, Supertype: null, "Fury", Energy: null, "Test Set", $"https://cards.test/{id}.png", Landscape: false);
}
