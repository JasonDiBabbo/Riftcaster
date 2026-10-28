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

    // A source that limits how often it may be asked (RiftHunt) says when: the schedule never goes before that.
    [Fact]
    public void DelayBeforeFirstFetch_NeverBeforeTheSourceAllows()
    {
        // Overdue, but the source may not be asked for another 5 hours.
        Assert.Equal(TimeSpan.FromHours(5), CardCatalogRefresher.DelayBeforeFirstFetch(Now - TimeSpan.FromDays(3), Now, notBefore: Now + TimeSpan.FromHours(5)));

        // Due in 23 hours, and the source allows it by then: the later of the two.
        Assert.Equal(TimeSpan.FromHours(23), CardCatalogRefresher.DelayBeforeFirstFetch(Now - TimeSpan.FromHours(1), Now, notBefore: Now + TimeSpan.FromHours(2)));
        Assert.Equal(TimeSpan.FromHours(30), CardCatalogRefresher.DelayBeforeFirstFetch(Now - TimeSpan.FromHours(1), Now, notBefore: Now + TimeSpan.FromHours(30)));
    }

    [Fact]
    public void DelayBeforeFirstFetch_ASourceThatMayBeAskedNow_ChangesNothing()
    {
        Assert.Equal(TimeSpan.Zero, CardCatalogRefresher.DelayBeforeFirstFetch(null, Now, notBefore: null));
        Assert.Equal(TimeSpan.Zero, CardCatalogRefresher.DelayBeforeFirstFetch(null, Now, notBefore: Now - TimeSpan.FromHours(1)));
    }

    [Fact]
    public void DelayAfterFetch_NeverBeforeTheSourceAllows()
    {
        // A failed fetch is retried in 15 minutes, but not by a source that says to wait three hours.
        Assert.Equal(TimeSpan.FromHours(3), CardCatalogRefresher.DelayAfterFetch(refreshed: false, Now + TimeSpan.FromHours(3), Now));
        Assert.Equal(TimeSpan.FromMinutes(15), CardCatalogRefresher.DelayAfterFetch(refreshed: false, Now + TimeSpan.FromMinutes(5), Now));
        Assert.Equal(TimeSpan.FromDays(1), CardCatalogRefresher.DelayAfterFetch(refreshed: true, Now + TimeSpan.FromHours(3), Now));
        Assert.Equal(TimeSpan.FromDays(2), CardCatalogRefresher.DelayAfterFetch(refreshed: true, Now + TimeSpan.FromDays(2), Now));
        Assert.Equal(TimeSpan.FromMinutes(15), CardCatalogRefresher.DelayAfterFetch(refreshed: false, notBefore: null, Now));
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

    // Switching to another source starts the wait again: the new catalog has its own age, and its source its own limits.
    [Fact]
    public async Task SwitchingToASourceWhoseCatalogIsOverdue_FetchesItAtOnce()
    {
        var time = new FakeTimeProvider(Now);
        var codexStore = new InMemoryCardStore();
        codexStore.Save(new CardCatalogSnapshot([Jinx], Now)); // Fresh: nothing to fetch for a day
        var hunt = new InMemoryCardSource { Cards = [Jinx] };
        var huntStore = new InMemoryCardStore();
        huntStore.Save(new CardCatalogSnapshot([Jinx], Now - TimeSpan.FromDays(3))); // Saved earlier, and overdue
        var catalog = new CardCatalog(
            new CardSourceSet((CardSourceKind.Riftcodex, _source, codexStore), (CardSourceKind.RiftHunt, hunt, huntStore)),
            CardSourceKind.Riftcodex,
            time,
            NullLogger<CardCatalog>.Instance);
        _refresher = new CardCatalogRefresher(catalog, time, NullLogger<CardCatalogRefresher>.Instance);
        await _refresher.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        Assert.Equal(0, _source.FetchCount); // Asleep until tomorrow

        await catalog.SwitchSourceAsync(CardSourceKind.RiftHunt);

        await WaitUntilAsync(() => hunt.FetchCount == 1);
        Assert.Equal(0, _source.FetchCount);
    }

    [Fact]
    public async Task AFetchOfASourceThatMayNotBeAskedYet_IsNotMade()
    {
        var time = new FakeTimeProvider(Now);
        var limited = new LimitedSource { NotBefore = Now + TimeSpan.FromHours(6) };
        var catalog = new CardCatalog(new InMemoryCardStore(), limited, time, NullLogger<CardCatalog>.Instance);
        _refresher = new CardCatalogRefresher(catalog, time, NullLogger<CardCatalogRefresher>.Instance);

        await _refresher.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        time.Advance(TimeSpan.FromHours(5));
        await Task.Delay(100);

        Assert.Equal(0, limited.FetchCount); // Asleep until the source allows it

        limited.NotBefore = null;
        time.Advance(TimeSpan.FromHours(2));
        await WaitUntilAsync(() => limited.FetchCount == 1);
    }

    private sealed class LimitedSource : ICardSource
    {
        private int _fetchCount;

        public DateTimeOffset? NotBefore { get; set; }

        public int FetchCount => _fetchCount;

        public Task<IReadOnlyList<Card>> FetchAllAsync(IProgress<CardFetchProgress>? progress, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _fetchCount);
            return Task.FromResult<IReadOnlyList<Card>>([Jinx]);
        }
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
