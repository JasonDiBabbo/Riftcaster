using Riftcaster.Contracts;

namespace Riftcaster.Core.Cards;

/// <summary>
/// Where the card catalogue comes from: the whole of it, every time.
/// </summary>
public interface ICardSource
{
    /// <summary>
    /// Fetches every card.
    /// </summary>
    /// <param name="cancellationToken">Cancels the fetch.</param>
    /// <returns>All the cards.</returns>
    /// <exception cref="HttpRequestException">The source couldn't be reached, or answered with an error.</exception>
    /// <exception cref="System.Text.Json.JsonException">The source answered with something that isn't a card list.</exception>
    Task<IReadOnlyList<Card>> FetchAllAsync(CancellationToken cancellationToken);
}
