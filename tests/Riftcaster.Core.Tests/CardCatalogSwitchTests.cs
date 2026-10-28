using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Riftcaster.Contracts;
using Riftcaster.Core.Cards;

namespace Riftcaster.Core.Tests;

// The catalog with two card sources: switching between them keeps each one's own saved catalog, leaves the catalog
// in use as it was until the new one is there, and keeps to a source's limits on being asked.
public class CardCatalogSwitchTests
{
    private static readonly Card Jinx = NewCard("1", "Jinx, Loose Cannon", CardType.Legend);
    private static readonly Card Poppy = NewCard("2", "Poppy, Paragon", CardType.Unit);
    private static readonly Card Recruit = NewCard("3", "Recruit", CardType.Unit);
    private static readonly Card HuntJinx = NewCard("h1", "Jinx, Loose Cannon", CardType.Legend);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero));

    private readonly FakeSource _codex = new() { Cards = [Jinx, Poppy, Recruit] };

    private readonly FakeSource _hunt = new() { Cards = [HuntJinx] };

    private readonly InMemoryCardStore _codexStore = new();

    private readonly InMemoryCardStore _huntStore = new();

    private CardCatalog Create(CardSourceKind active = CardSourceKind.Riftcodex) =>
        new(
            new CardSourceSet((CardSourceKind.Riftcodex, _codex, _codexStore), (CardSourceKind.RiftHunt, _hunt, _huntStore)),
            active,
            _time,
            NullLogger<CardCatalog>.Instance);

    private static Card NewCard(string id, string name, CardType type) =>
        new(id, $"tst-{id}", name, null, type, null, "Fury", Energy: null, "Test Set", $"https://cards.test/{id}.png", Landscape: false);

    [Fact]
    public void TheCatalog_StartsWithTheSavedCatalogOfTheSourceInUse()
    {
        _codexStore.Save(new CardCatalogSnapshot([Jinx], _time.GetUtcNow()));
        _huntStore.Save(new CardCatalogSnapshot([HuntJinx, Poppy], _time.GetUtcNow()));

        Assert.Equal([Jinx], Create(CardSourceKind.Riftcodex).Cards);
        Assert.Equal([HuntJinx, Poppy], Create(CardSourceKind.RiftHunt).Cards);
        Assert.Equal(CardSourceKind.RiftHunt, Create(CardSourceKind.RiftHunt).ActiveSource);
    }

    [Fact]
    public void ASourceThatIsntThere_IsReplacedByOneThatIs()
    {
        var catalog = new CardCatalog(new CardSourceSet((CardSourceKind.Riftcodex, _codex, _codexStore)), CardSourceKind.RiftHunt, _time, NullLogger<CardCatalog>.Instance);

        Assert.Equal(CardSourceKind.Riftcodex, catalog.ActiveSource);
        Assert.False(catalog.HasSource(CardSourceKind.RiftHunt));
    }

    [Fact]
    public async Task ASourceWithASavedCatalog_IsSwitchedToAtOnce_WithoutFetching()
    {
        _codexStore.Save(new CardCatalogSnapshot([Jinx], _time.GetUtcNow()));
        _huntStore.Save(new CardCatalogSnapshot([HuntJinx, Poppy], _time.GetUtcNow().AddHours(-3)));
        var catalog = Create();
        var changes = new List<string>();
        catalog.SourceChanged += () => changes.Add("source");
        catalog.Changed += () => changes.Add("cards");

        var result = await catalog.SwitchSourceAsync(CardSourceKind.RiftHunt);

        Assert.Equal(CardSwitchOutcome.Switched, result.Outcome);
        Assert.True(result.Done);
        Assert.Equal(CardSourceKind.RiftHunt, catalog.ActiveSource);
        Assert.Equal([HuntJinx, Poppy], catalog.Cards);
        Assert.Equal(_time.GetUtcNow().AddHours(-3), catalog.FetchedAt); // Its own age: a refresh follows when it's due
        Assert.Equal(0, _hunt.FetchCount);
        Assert.Equal(["source", "cards"], changes);
    }

    [Fact]
    public async Task ASourceWithoutASavedCatalog_IsFetchedThenSwitchedTo_AndSaved()
    {
        _codexStore.Save(new CardCatalogSnapshot([Jinx, Poppy, Recruit], _time.GetUtcNow()));
        var catalog = Create();
        var sourceChanges = 0;
        catalog.SourceChanged += () => sourceChanges++;

        var result = await catalog.SwitchSourceAsync(CardSourceKind.RiftHunt);

        Assert.Equal(CardSwitchOutcome.Switched, result.Outcome);
        Assert.Equal(CardSourceKind.RiftHunt, catalog.ActiveSource);
        Assert.Equal([HuntJinx], catalog.Cards);
        Assert.Equal(_time.GetUtcNow(), catalog.FetchedAt);
        Assert.Equal([HuntJinx], _huntStore.Snapshot!.Cards); // Its own saved catalog
        Assert.Equal([Jinx, Poppy, Recruit], _codexStore.Snapshot!.Cards); // The other is kept as it was
        Assert.Equal(1, sourceChanges);
        Assert.Equal(CardCatalogStatus.Idle, catalog.Status);
    }

    // A new source's catalog isn't expected to be like the one in use: the share it must have of it doesn't apply.
    [Fact]
    public async Task ASmallCatalogFromANewSource_IsNotHeldToTheShareOfTheOneInUse()
    {
        _codexStore.Save(new CardCatalogSnapshot([Jinx, Poppy, Recruit], _time.GetUtcNow()));
        var catalog = Create();

        var result = await catalog.SwitchSourceAsync(CardSourceKind.RiftHunt); // 1 card of 3: under half

        Assert.True(result.Done);
        Assert.Single(catalog.Cards);
    }

    [Fact]
    public async Task ARefresh_IsStillHeldToIt()
    {
        _codexStore.Save(new CardCatalogSnapshot([Jinx, Poppy, Recruit], _time.GetUtcNow()));
        var catalog = Create();
        _codex.Cards = [Jinx];

        var refreshed = await catalog.RefreshAsync(CancellationToken.None);

        Assert.False(refreshed);
        Assert.Equal(3, catalog.Cards.Count);
    }

    [Fact]
    public async Task WhileTheNewSourceDownloads_TheCatalogInUseStaysAsItIs_AndTheStatusSaysSo()
    {
        _codexStore.Save(new CardCatalogSnapshot([Jinx, Poppy, Recruit], _time.GetUtcNow()));
        var catalog = Create();
        _hunt.Gate = new TaskCompletionSource();
        _hunt.ProgressBeforeGate = new CardFetchProgress(1, 4);

        var switching = catalog.SwitchSourceAsync(CardSourceKind.RiftHunt);

        Assert.Equal(CardSourceKind.Riftcodex, catalog.ActiveSource);
        Assert.Equal(3, catalog.Cards.Count);
        Assert.Equal(CardSourceKind.RiftHunt, catalog.Status.SwitchingTo);
        Assert.True(catalog.Status.Fetching);
        Assert.Equal(new CardFetchProgress(1, 4), catalog.Status.Progress);

        _hunt.Gate.SetResult();
        await switching;
        Assert.Equal(CardSourceKind.RiftHunt, catalog.ActiveSource);
        Assert.Null(catalog.Status.SwitchingTo);
    }

    [Fact]
    public async Task AFailedSwitch_KeepsTheCatalog_AndSaysWhichSourceFailedAndWhy()
    {
        _codexStore.Save(new CardCatalogSnapshot([Jinx, Poppy, Recruit], _time.GetUtcNow()));
        var catalog = Create();
        _hunt.Failure = new HttpRequestException("No such host is known.");

        var result = await catalog.SwitchSourceAsync(CardSourceKind.RiftHunt);

        Assert.Equal(CardSwitchOutcome.Failed, result.Outcome);
        Assert.False(result.Done);
        Assert.Equal("No such host is known.", result.Problem);
        Assert.Equal(CardSourceKind.Riftcodex, catalog.ActiveSource);
        Assert.Equal(3, catalog.Cards.Count);
        Assert.Null(_huntStore.Snapshot); // Nothing saved for a source that wasn't fetched
        var failure = catalog.Status.LastFailure!;
        Assert.Equal(CardSourceKind.RiftHunt, failure.Source);
        Assert.Equal("No such host is known.", failure.Reason);
        Assert.Equal(_time.GetUtcNow(), failure.At);
        Assert.False(catalog.Status.Fetching);
    }

    [Theory]
    [InlineData(typeof(System.Text.Json.JsonException))]
    [InlineData(typeof(TaskCanceledException))] // A timeout
    public async Task EveryKindOfFetchFailure_IsAFailedSwitch(Type exceptionType)
    {
        var catalog = Create();
        _hunt.Failure = (Exception)Activator.CreateInstance(exceptionType, "It didn't work.")!;

        var result = await catalog.SwitchSourceAsync(CardSourceKind.RiftHunt);

        Assert.Equal(CardSwitchOutcome.Failed, result.Outcome);
    }

    [Fact]
    public async Task ASourceThatReturnsNoCards_IsAFailedSwitch()
    {
        var catalog = Create();
        _hunt.Cards = [];

        var result = await catalog.SwitchSourceAsync(CardSourceKind.RiftHunt);

        Assert.Equal(CardSwitchOutcome.Failed, result.Outcome);
        Assert.Equal(CardSourceKind.Riftcodex, catalog.ActiveSource);
        Assert.Null(_huntStore.Snapshot);
    }

    [Fact]
    public async Task AFailedSwitch_CanBeTriedAgain()
    {
        var catalog = Create();
        _hunt.Failure = new HttpRequestException("No internet.");
        await catalog.SwitchSourceAsync(CardSourceKind.RiftHunt);
        _hunt.Failure = null;

        var result = await catalog.SwitchSourceAsync(CardSourceKind.RiftHunt);

        Assert.True(result.Done);
        Assert.Null(catalog.Status.LastFailure);
    }

    [Fact]
    public async Task ASourceThatMayNotBeAskedYet_IsNotAsked()
    {
        var catalog = Create();
        _hunt.NotBefore = _time.GetUtcNow().AddHours(7);

        var result = await catalog.SwitchSourceAsync(CardSourceKind.RiftHunt);

        Assert.Equal(CardSwitchOutcome.TooSoon, result.Outcome);
        Assert.Equal(_time.GetUtcNow().AddHours(7), result.NotBefore);
        Assert.Equal(0, _hunt.FetchCount);
        Assert.Equal(CardSourceKind.Riftcodex, catalog.ActiveSource);
        Assert.Null(catalog.Status.LastFailure); // Not a failure: nothing was tried
    }

    // The wait is over by the time the source itself says so (it checks too).
    [Fact]
    public async Task ASourceThatRefusesAtTheLastMoment_IsTooSoonToo()
    {
        var catalog = Create();
        _hunt.Failure = new CardFetchNotAllowedYetException(_time.GetUtcNow().AddHours(2));

        var result = await catalog.SwitchSourceAsync(CardSourceKind.RiftHunt);

        Assert.Equal(CardSwitchOutcome.TooSoon, result.Outcome);
        Assert.Equal(_time.GetUtcNow().AddHours(2), result.NotBefore);
        Assert.Equal(CardCatalogStatus.Idle, catalog.Status);
    }

    // Its saved catalog is there, so there's nothing to ask for: the wait doesn't matter.
    [Fact]
    public async Task ASourceWithASavedCatalog_IsSwitchedToEvenWhileItMayNotBeAsked()
    {
        _huntStore.Save(new CardCatalogSnapshot([HuntJinx], _time.GetUtcNow()));
        _hunt.NotBefore = _time.GetUtcNow().AddHours(7);
        var catalog = Create();

        var result = await catalog.SwitchSourceAsync(CardSourceKind.RiftHunt);

        Assert.True(result.Done);
        Assert.Equal(_time.GetUtcNow().AddHours(7), catalog.NotBefore); // And it still may not be refreshed
    }

    [Fact]
    public async Task ASwitchCanBeCanceled_AndLeavesTheCatalogAsItWas()
    {
        _codexStore.Save(new CardCatalogSnapshot([Jinx, Poppy, Recruit], _time.GetUtcNow()));
        var catalog = Create();
        _hunt.Gate = new TaskCompletionSource();
        var switching = catalog.SwitchSourceAsync(CardSourceKind.RiftHunt);

        catalog.CancelSwitch();
        var result = await switching;

        Assert.Equal(CardSwitchOutcome.Canceled, result.Outcome);
        Assert.Equal(CardSourceKind.Riftcodex, catalog.ActiveSource);
        Assert.Equal(CardCatalogStatus.Idle, catalog.Status);
        Assert.Null(_huntStore.Snapshot);
    }

    [Fact]
    public async Task ASwitchCanBeCanceledByItsCaller()
    {
        var catalog = Create();
        _hunt.Gate = new TaskCompletionSource();
        using var cancel = new CancellationTokenSource();
        var switching = catalog.SwitchSourceAsync(CardSourceKind.RiftHunt, cancel.Token);

        await cancel.CancelAsync();

        Assert.Equal(CardSwitchOutcome.Canceled, (await switching).Outcome);
    }

    [Fact]
    public void CancelingWhenNothingIsSwitching_DoesNothing()
    {
        Create().CancelSwitch();
    }

    [Fact]
    public async Task ASwitchToTheSourceInUse_DoesNothing()
    {
        var catalog = Create();

        var result = await catalog.SwitchSourceAsync(CardSourceKind.Riftcodex);

        Assert.Equal(CardSwitchOutcome.AlreadyActive, result.Outcome);
        Assert.True(result.Done);
        Assert.Equal(0, _codex.FetchCount);
    }

    [Fact]
    public async Task ASwitchToASourceThatIsntThere_IsRefused()
    {
        var catalog = new CardCatalog(new CardSourceSet((CardSourceKind.Riftcodex, _codex, _codexStore)), CardSourceKind.Riftcodex, _time, NullLogger<CardCatalog>.Instance);

        var result = await catalog.SwitchSourceAsync(CardSourceKind.RiftHunt);

        Assert.Equal(CardSwitchOutcome.UnknownSource, result.Outcome);
    }

    // One fetch at a time: a switch while a refresh is running would fetch twice over.
    [Fact]
    public async Task ASwitchWhileARefreshIsRunning_WaitsItsTurn()
    {
        var catalog = Create();
        _codex.Gate = new TaskCompletionSource();
        var refreshing = catalog.RefreshAsync(CancellationToken.None);

        var result = await catalog.SwitchSourceAsync(CardSourceKind.RiftHunt);

        Assert.Equal(CardSwitchOutcome.Busy, result.Outcome);
        Assert.Equal(0, _hunt.FetchCount);
        _codex.Gate.SetResult();
        await refreshing;
    }

    [Fact]
    public async Task ARefreshWhileASwitchIsRunning_DoesNothing()
    {
        var catalog = Create();
        _hunt.Gate = new TaskCompletionSource();
        var switching = catalog.SwitchSourceAsync(CardSourceKind.RiftHunt);

        var refreshed = await catalog.RefreshAsync(CancellationToken.None);

        Assert.False(refreshed);
        Assert.Equal(0, _codex.FetchCount);
        _hunt.Gate.SetResult();
        await switching;
    }

    [Fact]
    public async Task ARefresh_IsOfTheSourceInUse_AndSavedToItsOwnStore()
    {
        _huntStore.Save(new CardCatalogSnapshot([HuntJinx], _time.GetUtcNow().AddDays(-2)));
        var catalog = Create(CardSourceKind.RiftHunt);
        _hunt.Cards = [HuntJinx, Poppy];

        var refreshed = await catalog.RefreshAsync(CancellationToken.None);

        Assert.True(refreshed);
        Assert.Equal([HuntJinx, Poppy], _huntStore.Snapshot!.Cards);
        Assert.Null(_codexStore.Snapshot);
        Assert.Equal(0, _codex.FetchCount);
    }

    [Fact]
    public async Task ARefresh_OfASourceThatMayNotBeAskedYet_FetchesNothing()
    {
        var catalog = Create();
        _codex.NotBefore = _time.GetUtcNow().AddHours(3);

        var refreshed = await catalog.RefreshAsync(CancellationToken.None);

        Assert.False(refreshed);
        Assert.Equal(0, _codex.FetchCount);
        Assert.Equal(CardCatalogStatus.Idle, catalog.Status);
        Assert.Equal(_time.GetUtcNow().AddHours(3), catalog.NotBefore);
    }

    [Fact]
    public async Task ARefresh_ThatTheSourceRefusesAtTheLastMoment_IsNotAFailure()
    {
        var catalog = Create();
        _codex.Failure = new CardFetchNotAllowedYetException(_time.GetUtcNow().AddHours(1));

        var refreshed = await catalog.RefreshAsync(CancellationToken.None);

        Assert.False(refreshed);
        Assert.Equal(CardCatalogStatus.Idle, catalog.Status);
    }

    [Fact]
    public void WhatAnotherSourceHasSaved_AndWhenItMayBeAsked_CanBeKnown()
    {
        _huntStore.Save(new CardCatalogSnapshot([HuntJinx], _time.GetUtcNow()));
        _hunt.NotBefore = _time.GetUtcNow().AddHours(5);
        var catalog = Create();

        Assert.True(catalog.HasSavedCatalog(CardSourceKind.RiftHunt));
        Assert.False(catalog.HasSavedCatalog(CardSourceKind.Riftcodex));
        Assert.Equal(_time.GetUtcNow().AddHours(5), catalog.NotBeforeFor(CardSourceKind.RiftHunt));
        Assert.Null(catalog.NotBeforeFor(CardSourceKind.Riftcodex));
        Assert.True(catalog.HasSource(CardSourceKind.RiftHunt));
    }

    [Fact]
    public void AnEmptySavedCatalog_IsNotASavedCatalog()
    {
        _huntStore.Save(new CardCatalogSnapshot([], _time.GetUtcNow()));

        Assert.False(Create().HasSavedCatalog(CardSourceKind.RiftHunt));
    }

    private sealed class FakeSource : ICardSource
    {
        public IReadOnlyList<Card> Cards { get; set; } = [];

        public Exception? Failure { get; set; }

        public TaskCompletionSource? Gate { get; set; }

        public CardFetchProgress? ProgressBeforeGate { get; set; }

        public DateTimeOffset? NotBefore { get; set; }

        public int FetchCount { get; private set; }

        public async Task<IReadOnlyList<Card>> FetchAllAsync(IProgress<CardFetchProgress>? progress, CancellationToken cancellationToken)
        {
            FetchCount++;
            if (Gate is not null)
            {
                if (ProgressBeforeGate is not null)
                {
                    progress?.Report(ProgressBeforeGate);
                }

                await Gate.Task.WaitAsync(cancellationToken);
            }

            return Failure is null ? Cards : throw Failure;
        }
    }
}
