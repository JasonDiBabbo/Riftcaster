using Riftcaster.Contracts;

namespace Riftcaster.Core.Cards;

/// <summary>
/// Where the card catalog comes from: the whole of it, every time.
/// </summary>
public interface ICardSource
{
    /// <summary>
    /// Fetches every card.
    /// </summary>
    /// <param name="progress">
    /// Told how far the fetch has got as it goes (a whole fetch can take minutes), or <see langword="null"/>.
    /// </param>
    /// <param name="cancellationToken">Cancels the fetch.</param>
    /// <returns>All the cards.</returns>
    /// <exception cref="HttpRequestException">The source couldn't be reached, or answered with an error.</exception>
    /// <exception cref="System.Text.Json.JsonException">The source answered with something that isn't a card list.</exception>
    /// <exception cref="CardFetchNotAllowedYetException">The source's wait (see <see cref="NotBefore"/>) isn't over.</exception>
    Task<IReadOnlyList<Card>> FetchAllAsync(IProgress<CardFetchProgress>? progress, CancellationToken cancellationToken);

    /// <summary>
    /// The earliest time the source may be asked for its cards, or <see langword="null"/> for any time.
    /// </summary>
    /// <remarks>
    /// For a source that asks to be fetched no more than once a day: it enforces that itself (asking early throws
    /// <see cref="CardFetchNotAllowedYetException"/>), and says here when it may be asked, so nothing asks early.
    /// </remarks>
    DateTimeOffset? NotBefore => null;

    /// <summary>
    /// Whether the cards are a small built-in sample standing in for the real source, as in Development, so the web
    /// interface can say so.
    /// </summary>
    bool IsSample => false;
}
