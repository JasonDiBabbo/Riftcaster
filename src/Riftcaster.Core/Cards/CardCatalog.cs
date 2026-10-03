using System.Text.Json;
using Microsoft.Extensions.Logging;
using Riftcaster.Contracts;

namespace Riftcaster.Core.Cards;

/// <summary>
/// Every Riftbound card: searched by the Featured card panel, and listed by the Players panel.
/// </summary>
/// <remarks>
/// Starts with the last saved catalogue, so it works at once and without internet. <see cref="RefreshAsync"/>
/// fetches a new one (slowly: the whole catalogue can take minutes) and swaps it in; if the fetch fails,
/// the catalogue keeps what it has.
/// </remarks>
public sealed class CardCatalog
{
    /// <summary>
    /// How many results <see cref="Search"/> returns unless asked for a different number.
    /// </summary>
    public const int DefaultSearchLimit = 50;

    /// <summary>
    /// The smallest share of the current catalogue a fetched one may have. A fetch that brings fewer
    /// cards than this is more likely a broken or cut-short answer than cards actually removed, so
    /// the catalogue keeps what it has.
    /// </summary>
    public const double SmallestAcceptedShare = 0.5;

    private readonly ICardStore _store;

    private readonly ICardSource _source;

    private readonly TimeProvider _time;

    private readonly ILogger<CardCatalog> _logger;

    // One refresh at a time: a second one while the first is running would only repeat its work.
    private readonly SemaphoreSlim _refreshing = new(1, 1);

    // Replaced whole, never changed, so a reader always sees one consistent catalogue.
    private CardIndex _index;

    // Replaced whole too. Only a refresh changes it, and only one runs at a time.
    private CardCatalogStatus _status = CardCatalogStatus.Idle;

    /// <summary>
    /// Creates the catalogue with the last saved cards, if any.
    /// </summary>
    /// <param name="store">Keeps the last fetched catalogue.</param>
    /// <param name="source">Where new catalogues come from.</param>
    /// <param name="time">The clock, for when a catalogue was fetched.</param>
    /// <param name="logger">Logs failed refreshes.</param>
    public CardCatalog(ICardStore store, ICardSource source, TimeProvider time, ILogger<CardCatalog> logger)
    {
        _store = store;
        _source = source;
        _time = time;
        _logger = logger;
        _index = CardIndex.Build(store.Load());
    }

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
    /// Fetches the whole catalogue and swaps it in, then saves it. Raises <see cref="Changed"/> if
    /// the cards differ from before.
    /// </summary>
    /// <remarks>
    /// Never throws for a failed fetch (unreachable, an error, a timeout, or an empty or suspiciously
    /// small catalogue: see <see cref="SmallestAcceptedShare"/>): it logs it and keeps the cards it
    /// has. Does nothing if a refresh is already running.
    /// </remarks>
    /// <param name="cancellationToken">Cancels the fetch, e.g. when the server shuts down.</param>
    /// <returns>Whether a new catalogue was swapped in.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<bool> RefreshAsync(CancellationToken cancellationToken)
    {
        if (!await _refreshing.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            return false; // Another refresh is already fetching
        }

        try
        {
            SetStatus(_status with { Fetching = true, Progress = null });

            IReadOnlyList<Card> cards;
            try
            {
                cards = await _source.FetchAllAsync(new Reporter(progress => SetStatus(_status with { Progress = progress })), cancellationToken);
            }
            catch (Exception exception) when (IsFetchFailure(exception) && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(exception, "Couldn't fetch the card catalogue; keeping the {Count} cards already loaded.", Cards.Count);
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
            _store.Save(snapshot); // Even when unchanged, so the saved FetchedAt stays current
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
                SetStatus(_status with { Fetching = false, Progress = null }); // Cancelled: no new failure to report
            }

            _refreshing.Release();
        }
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
    // even though nobody cancelled; RefreshAsync tells the two apart with the token.
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
    /// One catalogue, with the lookups worked out once when it's loaded rather than on every use.
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
