using Riftcaster.Contracts;

namespace Riftcaster.Admin.Players;

/// <summary>
/// Which cards a player card's selects offer.
/// </summary>
public static class CardChoices
{
    /// <summary>
    /// The champions to offer for a legend: the legend's own champion's cards (e.g. the Jinx units
    /// for "Jinx, Loose Cannon"), since a deck's champion must match its legend. Every champion when
    /// no legend is chosen, or when none match (so an unusual name never leaves the list empty).
    /// </summary>
    /// <param name="legend">The player's legend, or <see langword="null"/>.</param>
    /// <param name="champions">Every champion unit.</param>
    public static IReadOnlyList<Card> ChampionsFor(Card? legend, IReadOnlyList<Card> champions)
    {
        // Legend names are "Champion, Title" (see RiftcodexNames).
        if (legend?.Name.Split(", ", 2) is not [var champion, _])
        {
            return champions;
        }

        var matching = champions.Where(card => card.Name.StartsWith($"{champion}, ", StringComparison.OrdinalIgnoreCase)).ToList();
        return matching.Count > 0 ? matching : champions;
    }
}
