using Riftcaster.Contracts;

namespace Riftcaster.Core.Cards;

/// <summary>
/// A card source that returns a fixed list instead of fetching. For tests, so they never call
/// the real card database.
/// </summary>
public sealed class InMemoryCardSource : ICardSource
{
    /// <summary>
    /// The cards each fetch returns. Empty by default, which the catalogue treats as a failed fetch.
    /// </summary>
    public IReadOnlyList<Card> Cards { get; set; } = [];

    /// <summary>
    /// How many times <see cref="FetchAllAsync"/> has been called, so tests can check when the catalogue fetches.
    /// </summary>
    public int FetchCount => _fetchCount;

    // Interlocked: tests read it from one thread while the refresher fetches on another.
    private int _fetchCount;

    /// <inheritdoc/>
    public Task<IReadOnlyList<Card>> FetchAllAsync(IProgress<CardFetchProgress>? progress, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _fetchCount);
        return Task.FromResult(Cards);
    }
}
