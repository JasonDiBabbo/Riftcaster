using System.Text.Json;
using Microsoft.Extensions.Logging;
using Riftcaster.Contracts;

namespace Riftcaster.Core.Cards;

/// <summary>
/// Every Riftbound card: searched by the Featured card panel, and listed by the Players panel.
/// </summary>
/// <remarks>
/// <para>
/// Starts with the last saved catalog of the source in use, so it works at once and without internet.
/// <see cref="RefreshAsync"/> fetches a new one (slowly: the whole catalog can take minutes) and swaps it in; if
/// the fetch fails, the catalog keeps what it has.
/// </para>
/// <para>
/// There can be several sources (see <see cref="CardSourceSet"/>), each keeping its own saved catalog.
/// <see cref="SwitchSourceAsync"/> moves to another: at once if it has a saved catalog, else once its catalog has
/// been fetched, with the catalog in use as it was until then.
/// </para>
/// </remarks>
public sealed class CardCatalog
{
    /// <summary>
    /// How many results <see cref="Search"/> returns unless asked for a different number.
    /// </summary>
    public const int DefaultSearchLimit = 50;

    /// <summary>
    /// The smallest share of the current catalog a fetched one may have. A fetch that brings fewer
    /// cards than this is more likely a broken or cut-short answer than cards actually removed, so
    /// the catalog keeps what it has. (Not applied to a switch to another source, whose catalog is
    /// not expected to match the one in use.)
    /// </summary>
    public const double SmallestAcceptedShare = 0.5;

    private readonly CardSourceSet _sources;

    private readonly TimeProvider _time;

    private readonly ILogger<CardCatalog> _logger;

    // One fetch at a time, a refresh or a switch: a second one while the first is running would only repeat its work.
    private readonly SemaphoreSlim _refreshing = new(1, 1);

    // Set while a switch is fetching, so it can be canceled.
    private readonly Lock _switchLock = new();

    private CancellationTokenSource? _switching;

    // The source the catalog is in use from. Changed only by a switch, which holds _refreshing.
    private CardSourceKind _active;

    // Replaced whole, never changed, so a reader always sees one consistent catalog.
    private CardIndex _index;

    // Replaced whole too. Only a refresh or a switch changes it, and only one runs at a time.
    private CardCatalogStatus _status = CardCatalogStatus.Idle;

    /// <summary>
    /// Creates a catalog with one source, with the last saved cards, if any.
    /// </summary>
    /// <param name="store">Keeps the last fetched catalog.</param>
    /// <param name="source">Where new catalogs come from.</param>
    /// <param name="time">The clock, for when a catalog was fetched.</param>
    /// <param name="logger">Logs failed refreshes.</param>
    public CardCatalog(ICardStore store, ICardSource source, TimeProvider time, ILogger<CardCatalog> logger)
        : this(new CardSourceSet((CardSourceKind.Riftcodex, source, store)), CardSourceKind.Riftcodex, time, logger)
    {
    }

    /// <summary>
    /// Creates a catalog with several sources, in use from one of them, with its last saved cards, if any.
    /// </summary>
    /// <param name="sources">The sources, each with the store that keeps its catalog.</param>
    /// <param name="active">The source in use. If there is no such source, the first one.</param>
    /// <param name="time">The clock, for when a catalog was fetched.</param>
    /// <param name="logger">Logs failed refreshes.</param>
    public CardCatalog(CardSourceSet sources, CardSourceKind active, TimeProvider time, ILogger<CardCatalog> logger)
    {
        _sources = sources;
        _active = sources.Has(active) ? active : Enum.GetValues<CardSourceKind>().First(sources.Has);
        _time = time;
        _logger = logger;
        _index = CardIndex.Build(sources.Store(_active).Load());
    }

    /// <summary>
    /// The source the catalog is in use from.
    /// </summary>
    public CardSourceKind ActiveSource => _active;

    /// <summary>
    /// Whether there is a source of a kind to switch to.
    /// </summary>
    /// <param name="kind">The kind.</param>
    public bool HasSource(CardSourceKind kind) => _sources.Has(kind);

    /// <summary>
    /// The earliest time the source in use may be asked for a new catalog, or <see langword="null"/> for any time.
    /// </summary>
    public DateTimeOffset? NotBefore => _sources.Source(_active).NotBefore;

    /// <summary>
    /// The earliest time another source may be asked for its catalog, or <see langword="null"/> for any time.
    /// </summary>
    /// <param name="kind">The source.</param>
    public DateTimeOffset? NotBeforeFor(CardSourceKind kind) => _sources.Has(kind) ? _sources.Source(kind).NotBefore : null;

    /// <summary>
    /// Whether a source is a small built-in sample standing in for the real one (see <see cref="ICardSource.IsSample"/>).
    /// </summary>
    /// <param name="kind">The source.</param>
    public bool IsSample(CardSourceKind kind) => _sources.Has(kind) && _sources.Source(kind).IsSample;

    /// <summary>
    /// Whether another source has a saved catalog, so that switching to it is instant.
    /// </summary>
    /// <param name="kind">The source.</param>
    public bool HasSavedCatalog(CardSourceKind kind) => _sources.Has(kind) && _sources.Store(kind).Load() is { Cards.Count: > 0 };

    /// <summary>
    /// Every card, in the source's order. Empty until the first fetch if nothing was saved.
    /// </summary>
    public IReadOnlyList<Card> Cards => _index.Cards;

    /// <summary>
    /// When the cards were fetched, or <see langword="null"/> if there are none yet.
    /// </summary>
    public DateTimeOffset? FetchedAt => _index.FetchedAt;

    /// <summary>
    /// Legends, by name, for the Players panel: one printing of each (see <see cref="Battlefields"/>).
    /// </summary>
    public IReadOnlyList<Card> Legends => _index.Legends;

    /// <summary>
    /// Champion units (the cards a player chooses as their champion), by name, for the Players panel:
    /// one printing of each (see <see cref="Battlefields"/>).
    /// </summary>
    public IReadOnlyList<Card> ChampionUnits => _index.ChampionUnits;

    /// <summary>
    /// Battlefields, by name, for the Players panel: one printing of each, the standard one where
    /// there is one, since the panel chooses a card, not a printing.
    /// </summary>
    public IReadOnlyList<Card> Battlefields => _index.Battlefields;

    /// <summary>
    /// Whether a fetch is running and how far it has got, and why the last one failed, if it did.
    /// </summary>
    public CardCatalogStatus Status => _status;

    /// <summary>
    /// Raised after a refresh brings different cards.
    /// </summary>
    /// <remarks>
    /// May be raised on any thread.
    /// </remarks>
    public event Action? Changed;

    /// <summary>
    /// Raised after a switch moves the catalog to another source.
    /// </summary>
    /// <remarks>
    /// May be raised on any thread.
    /// </remarks>
    public event Action? SourceChanged;

    /// <summary>
    /// Raised after <see cref="Status"/> changes: when a fetch starts, at each page, and when it ends.
    /// </summary>
    /// <remarks>
    /// May be raised on any thread.
    /// </remarks>
    public event Action? StatusChanged;

    /// <summary>
    /// Finds a card by its id.
    /// </summary>
    /// <param name="id">The card's id.</param>
    /// <returns>The card, or <see langword="null"/> if there's no card with that id.</returns>
    public Card? Find(string id) => _index.ById.GetValueOrDefault(id);

    /// <summary>
    /// Finds the card in this catalog that is the same card as one from another source, which has other ids:
    /// the one with the same name and variant (the same collector code first, where several have), else the
    /// standard printing of that name.
    /// </summary>
    /// <param name="card">The card, from another catalog.</param>
    /// <returns>The card here, or <see langword="null"/> if there's no card with that name.</returns>
    public Card? FindEquivalent(Card card)
    {
        var sameName = _index.ByName.Where(other => other.Name.Equals(card.Name, StringComparison.OrdinalIgnoreCase)).ToList();
        var sameVariant = sameName.Where(other => string.Equals(other.Variant, card.Variant, StringComparison.OrdinalIgnoreCase)).ToList();
        return sameVariant.FirstOrDefault(other => other.Code.Equals(card.Code, StringComparison.OrdinalIgnoreCase))
            ?? sameVariant.FirstOrDefault()
            ?? sameName.FirstOrDefault(other => other.Variant is null);
    }

    /// <summary>
    /// Finds cards whose name, type or domain contains the query, ignoring case. Cards whose name
    /// starts with it come first, then other name matches, then type and domain matches, each by name.
    /// </summary>
    /// <param name="query">What the operator typed. Surrounding spaces are ignored.</param>
    /// <param name="limit">The most results to return.</param>
    /// <returns>The matching cards, best first; none for an empty query.</returns>
    public IReadOnlyList<Card> Search(string? query, int limit = DefaultSearchLimit)
    {
        query = query?.Trim();
        if (string.IsNullOrEmpty(query))
        {
            return [];
        }

        return _index.ByName
            .Select(card => (Card: card, Rank: Rank(card, query)))
            .Where(match => match.Rank is not null)
            .OrderBy(match => match.Rank) // Stable: cards with the same rank stay in name order
            .Take(limit)
            .Select(match => match.Card)
            .ToList();
    }

    /// <summary>
    /// Fetches the whole catalog from the source in use and swaps it in, then saves it. Raises <see cref="Changed"/>
    /// if the cards differ from before.
    /// </summary>
    /// <remarks>
    /// Never throws for a failed fetch (unreachable, an error, a timeout, or an empty or suspiciously
    /// small catalog: see <see cref="SmallestAcceptedShare"/>): it logs it and keeps the cards it
    /// has. Does nothing if a fetch is already running, or the source may not be asked yet (see <see cref="NotBefore"/>).
    /// </remarks>
    /// <param name="cancellationToken">Cancels the fetch, e.g. when the server shuts down.</param>
    /// <returns>Whether a new catalog was swapped in.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public async Task<bool> RefreshAsync(CancellationToken cancellationToken)
    {
        if (!await _refreshing.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            return false; // Another fetch is already running
        }

        try
        {
            var source = _sources.Source(_active);
            var store = _sources.Store(_active);
            if (source.NotBefore is { } notBefore)
            {
                _logger.LogInformation("Not fetching the card catalog: its source may not be asked again until {NotBefore:u}.", notBefore);
                return false;
            }

            SetStatus(_status with { Fetching = true, Progress = null });

            IReadOnlyList<Card> cards;
            try
            {
                cards = await source.FetchAllAsync(new Reporter(progress => SetStatus(_status with { Progress = progress })), cancellationToken);
            }
            catch (CardFetchNotAllowedYetException exception)
            {
                _logger.LogInformation("Not fetching the card catalog: its source may not be asked again until {NotBefore:u}.", exception.NotBefore);
                return false;
            }
            catch (Exception exception) when (IsFetchFailure(exception) && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(exception, "Couldn't fetch the card catalog; keeping the {Count} cards already loaded.", Cards.Count);
                Fail(exception.Message);
                return false;
            }

            if (cards.Count == 0)
            {
                _logger.LogWarning("The card source returned no cards; keeping the {Count} cards already loaded.", Cards.Count);
                Fail("The card source returned no cards.");
                return false;
            }

            if (cards.Count < Cards.Count * SmallestAcceptedShare)
            {
                _logger.LogWarning(
                    "The card source returned only {Fetched} cards, well short of the {Count} already loaded; keeping those.",
                    cards.Count, Cards.Count);
                Fail($"The card source returned only {cards.Count} cards, well short of the {Cards.Count} already loaded.");
                return false;
            }

            var changed = !cards.SequenceEqual(Cards);
            var snapshot = new CardCatalogSnapshot(cards, _time.GetUtcNow());
            _index = CardIndex.Build(snapshot);
            store.Save(snapshot); // Even when unchanged, so the saved FetchedAt stays current
            SetStatus(CardCatalogStatus.Idle);

            if (changed)
            {
                Changed?.Invoke();
            }

            return true;
        }
        finally
        {
            if (_status.Fetching)
            {
                SetStatus(_status with { Fetching = false, Progress = null }); // Canceled: no new failure to report
            }

            _refreshing.Release();
        }
    }

    /// <summary>
    /// Moves the catalog to another source. If that source has a saved catalog, it's in use at once (a refresh
    /// follows when it's due); otherwise its catalog is fetched first, and the catalog in use stays as it is until
    /// then, and after if the fetch fails.
    /// </summary>
    /// <remarks>
    /// Doesn't ask for more than the source allows: one that may not be asked yet isn't (<see cref="CardSwitchOutcome.TooSoon"/>).
    /// A catalog from a new source isn't held to <see cref="SmallestAcceptedShare"/>, only to not being empty.
    /// </remarks>
    /// <param name="target">The source to move to.</param>
    /// <param name="cancellationToken">Cancels the switch, like <see cref="CancelSwitch"/>.</param>
    /// <returns>How it ended.</returns>
    public async Task<CardSwitchResult> SwitchSourceAsync(CardSourceKind target, CancellationToken cancellationToken = default)
    {
        if (!_sources.Has(target))
        {
            return new CardSwitchResult(CardSwitchOutcome.UnknownSource);
        }

        if (target == _active)
        {
            return new CardSwitchResult(CardSwitchOutcome.AlreadyActive);
        }

        if (!await _refreshing.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            return new CardSwitchResult(CardSwitchOutcome.Busy, "The card catalog is being fetched. Try again when it's done.");
        }

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (_switchLock)
        {
            _switching = cancellation;
        }

        try
        {
            var source = _sources.Source(target);
            var store = _sources.Store(target);

            // Fetched before, and kept: no waiting.
            if (store.Load() is { Cards.Count: > 0 } saved)
            {
                Adopt(target, saved);
                return new CardSwitchResult(CardSwitchOutcome.Switched);
            }

            if (source.NotBefore is { } notBefore)
            {
                return TooSoon(target, notBefore);
            }

            SetStatus(new CardCatalogStatus(Fetching: true, Progress: null, LastFailure: null, SwitchingTo: target));

            IReadOnlyList<Card> cards;
            try
            {
                cards = await source.FetchAllAsync(new Reporter(progress => SetStatus(_status with { Progress = progress })), cancellation.Token);
            }
            catch (CardFetchNotAllowedYetException exception)
            {
                return TooSoon(target, exception.NotBefore);
            }
            catch (Exception exception) when (IsFetchFailure(exception) && !cancellation.IsCancellationRequested)
            {
                _logger.LogWarning(exception, "Couldn't fetch the {Source} card catalog; keeping the {Count} cards already loaded.", target, Cards.Count);
                return Failed(target, exception.Message);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                _logger.LogInformation("Switching to the {Source} card catalog was canceled.", target);
                SetStatus(CardCatalogStatus.Idle);
                return new CardSwitchResult(CardSwitchOutcome.Canceled);
            }

            if (cards.Count == 0)
            {
                _logger.LogWarning("The {Source} card source returned no cards; keeping the {Count} cards already loaded.", target, Cards.Count);
                return Failed(target, "The card source returned no cards.");
            }

            var snapshot = new CardCatalogSnapshot(cards, _time.GetUtcNow());
            store.Save(snapshot);
            Adopt(target, snapshot);
            return new CardSwitchResult(CardSwitchOutcome.Switched);
        }
        finally
        {
            lock (_switchLock)
            {
                _switching = null;
            }

            if (_status.Fetching)
            {
                SetStatus(_status with { Fetching = false, Progress = null, SwitchingTo = null });
            }

            _refreshing.Release();
        }
    }

    /// <summary>
    /// Cancels a switch that is fetching its catalog. Does nothing if none is.
    /// </summary>
    public void CancelSwitch()
    {
        lock (_switchLock)
        {
            _switching?.Cancel();
        }
    }

    // The catalog of a source, in use from now on.
    private void Adopt(CardSourceKind source, CardCatalogSnapshot snapshot)
    {
        _active = source;
        _index = CardIndex.Build(snapshot);
        SetStatus(CardCatalogStatus.Idle);
        _logger.LogInformation("The card catalog is now from {Source}: {Count} cards.", source, snapshot.Cards.Count);
        SourceChanged?.Invoke();
        Changed?.Invoke();
    }

    private CardSwitchResult Failed(CardSourceKind target, string reason)
    {
        SetStatus(new CardCatalogStatus(Fetching: false, Progress: null, new CardFetchFailure(_time.GetUtcNow(), reason, target)));
        return new CardSwitchResult(CardSwitchOutcome.Failed, reason);
    }

    private CardSwitchResult TooSoon(CardSourceKind target, DateTimeOffset notBefore)
    {
        var reason = $"The {target} source may not be asked again until {notBefore:u}.";
        _logger.LogInformation("Not switching to the {Source} card catalog: {Reason}", target, reason);
        SetStatus(CardCatalogStatus.Idle);
        return new CardSwitchResult(CardSwitchOutcome.TooSoon, reason, notBefore);
    }

    private void SetStatus(CardCatalogStatus status)
    {
        _status = status;
        StatusChanged?.Invoke();
    }

    private void Fail(string reason)
    {
        SetStatus(new CardCatalogStatus(Fetching: false, Progress: null, new CardFetchFailure(_time.GetUtcNow(), reason)));
    }

    // HttpClient's own timeout surfaces as a TaskCanceledException (an OperationCanceledException)
    // even though nobody canceled; RefreshAsync tells the two apart with the token.
    private static bool IsFetchFailure(Exception exception) =>
        exception is HttpRequestException or JsonException or OperationCanceledException;

    // 0 for a name starting with the query, 1 for a name containing it, 2 for a type or domain
    // containing it, or null for no match.
    private static int? Rank(Card card, string query) =>
        card.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 0
        : card.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ? 1
        : card.Type.ToString().Contains(query, StringComparison.OrdinalIgnoreCase)
            || card.Domain.Contains(query, StringComparison.OrdinalIgnoreCase) ? 2
        : null;

    /// <summary>
    /// Passes each report straight on. Unlike <see cref="Progress{T}"/>, which hands reports to the
    /// thread pool, so they could arrive out of order or after the fetch has ended.
    /// </summary>
    private sealed class Reporter(Action<CardFetchProgress> report) : IProgress<CardFetchProgress>
    {
        public void Report(CardFetchProgress value) => report(value);
    }

    /// <summary>
    /// One catalog, with the lookups worked out once when it's loaded rather than on every use.
    /// </summary>
    private sealed record CardIndex(
        IReadOnlyList<Card> Cards,
        DateTimeOffset? FetchedAt,
        IReadOnlyList<Card> ByName,
        IReadOnlyDictionary<string, Card> ById,
        IReadOnlyList<Card> Legends,
        IReadOnlyList<Card> ChampionUnits,
        IReadOnlyList<Card> Battlefields)
    {
        public static CardIndex Build(CardCatalogSnapshot? snapshot)
        {
            var cards = snapshot?.Cards ?? [];

            // By name, then variant (the standard printing, with no variant, first).
            var byName = cards
                .OrderBy(card => card.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(card => card.Variant ?? "", StringComparer.OrdinalIgnoreCase)
                .ToList();

            // One printing per name, for the Players panel's lists. ByName puts the standard printing
            // of each name first.
            IReadOnlyList<Card> OnePerName(Func<Card, bool> include) =>
                [.. byName.Where(include).DistinctBy(card => card.Name, StringComparer.OrdinalIgnoreCase)];

            return new CardIndex(
                cards,
                snapshot?.FetchedAt,
                byName,
                cards.DistinctBy(card => card.Id).ToDictionary(card => card.Id),
                OnePerName(card => card.Type == CardType.Legend),
                OnePerName(card => card.Type == CardType.Unit && card.Supertype == CardSupertype.Champion),
                OnePerName(card => card.Type == CardType.Battlefield));
        }
    }
}
