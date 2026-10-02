using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Riftcaster.Contracts;
using Riftcaster.Core.Cards;

namespace Riftcaster.Server.Tests;

public sealed class CardCatalogRefresherTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    private static readonly Card Jinx = new("1", "ogn-251-298", "Jinx, Loose Cannon", null, CardType.Legend, null, "Fury / Chaos", null, "Origins", "https://cards.test/1.png", Landscape: false);

    private readonly InMemoryCardStore _store = new();

    private readonly InMemoryCardSource _source = new() { Cards = [Jinx] };

    private CardCatalogRefresher? _refresher;

    public async ValueTask DisposeAsync()
    {
        if (_refresher is not null)
        {
            await _refresher.StopAsync(CancellationToken.None);
            _refresher.Dispose();
        }
    }

    // The schedule, as plain values: no threads or clocks involved, so no timing to go wrong.
    [Fact]
    public void DelayBeforeFirstFetch_NeverFetched_IsNone()
    {
        Assert.Equal(TimeSpan.Zero, CardCatalogRefresher.DelayBeforeFirstFetch(fetchedAt: null, Now));
    }

    [Fact]
    public void DelayBeforeFirstFetch_FetchedRecently_WaitsUntilADayAfter()
    {
        var delay = CardCatalogRefresher.DelayBeforeFirstFetch(Now - TimeSpan.FromHours(1), Now);

        Assert.Equal(TimeSpan.FromHours(23), delay);
    }

    [Fact]
    public void DelayBeforeFirstFetch_Overdue_IsNone()
    {
        Assert.Equal(TimeSpan.Zero, CardCatalogRefresher.DelayBeforeFirstFetch(Now - TimeSpan.FromDays(3), Now));
    }

    [Fact]
    public void DelayAfterFetch_RetriesAFailedFetchSooner()
    {
        Assert.Equal(TimeSpan.FromDays(1), CardCatalogRefresher.DelayAfterFetch(refreshed: true));
        Assert.Equal(TimeSpan.FromMinutes(15), CardCatalogRefresher.DelayAfterFetch(refreshed: false));
    }

    // The service itself, for what doesn't depend on timing.
    [Fact]
    public async Task NothingSaved_FetchesAtOnce()
    {
        var catalog = await StartAsync();

        await WaitUntilAsync(() => catalog.Cards.Count == 1);

        Assert.Equal(1, _source.FetchCount);
    }

    [Fact]
    public async Task Stopping_EndsCleanly()
    {
        await StartAsync();
        await WaitUntilAsync(() => _source.FetchCount == 1);

        await _refresher!.StopAsync(CancellationToken.None);

        Assert.True(_refresher.ExecuteTask!.IsCompletedSuccessfully);
    }

    private async Task<CardCatalog> StartAsync()
    {
        var time = new FakeTimeProvider(Now);
        var catalog = new CardCatalog(_store, _source, time, NullLogger<CardCatalog>.Instance);
        _refresher = new CardCatalogRefresher(catalog, time, NullLogger<CardCatalogRefresher>.Instance);
        await _refresher.StartAsync(CancellationToken.None);
        return catalog;
    }

    // The refresher runs on its own, so wait (briefly, in real time) for it to get somewhere.
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}
