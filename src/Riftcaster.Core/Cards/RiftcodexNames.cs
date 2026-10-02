using System.Text.RegularExpressions;
using Riftcaster.Contracts;

namespace Riftcaster.Core.Cards;

/// <summary>
/// Turns Riftcodex's card names into the form the admin and overlays show: "Champion, Title",
/// with any variant (e.g. "Alternate Art") split off. The same repairs as the first version of
/// this application (its scripts/fetch-cards.mjs), plus splitting off the variant, which it didn't
/// need because it left most variants out.
/// </summary>
public static partial class RiftcodexNames
{
    // Tags that aren't a champion's name, so can't start a legend's name (e.g. "Yordle" on Kennen).
    private static readonly HashSet<string> NonChampionTags = ["Yordle", "Unit", "Spell", "Gear", "Champion"];

    /// <summary>
    /// Normalizes a card's name.
    /// </summary>
    /// <param name="name">Riftcodex's name, e.g. "Vi - Piltover Enforcer (Signature)".</param>
    /// <param name="type">The card's type: legends get their champion added if it's missing.</param>
    /// <param name="tags">The card's tags, which for a legend include its champion.</param>
    /// <returns>
    /// The name as shown, e.g. "Vi, Piltover Enforcer", and the variant, e.g. "Signature", or
    /// <see langword="null"/> for the standard printing.
    /// </returns>
    public static (string Name, string? Variant) Normalize(string name, CardType type, IReadOnlyList<string>? tags)
    {
        name = name.Trim();

        // "Poppy - Paragon (Alternate Art)": the bracketed end is the variant, not part of the name.
        string? variant = null;
        if (VariantSuffix().Match(name) is { Success: true } match)
        {
            variant = match.Groups[1].Value;
            name = name[..match.Index];
        }

        // Riftcodex writes most champion cards "Champion - Title", and some "Champion, Title".
        name = name.Replace(" - ", ", ");

        // Some legends are named by their title alone ("Master of Shadows"); their champion is a tag.
        if (type == CardType.Legend && !name.Contains(", ")
            && tags?.FirstOrDefault(tag => !NonChampionTags.Contains(tag)) is { } champion)
        {
            name = $"{champion}, {name}";
        }

        return (name, variant);
    }

    // A space and a bracketed suffix at the very end, e.g. " (Alternate Art)". Not "(271) // Buff",
    // where the brackets are part of a token's name.
    [GeneratedRegex(@" \(([^()]+)\)$")]
    private static partial Regex VariantSuffix();
}
