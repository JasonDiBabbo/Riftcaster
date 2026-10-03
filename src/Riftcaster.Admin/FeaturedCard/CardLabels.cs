using Riftcaster.Contracts;

namespace Riftcaster.Admin.FeaturedCard;

/// <summary>
/// How cards are described in the admin.
/// </summary>
public static class CardLabels
{
    /// <summary>
    /// The card's type, with its supertype if it has one, e.g. "Champion Unit" or "Legend".
    /// </summary>
    public static string Type(Card card) =>
        card.Supertype is { } supertype and not CardSupertype.Other ? $"{supertype} {card.Type}" : card.Type.ToString();

    /// <summary>
    /// A search result's second line: the type, the variant if it isn't the standard printing, and
    /// the set, e.g. "Legend · Metal · Origins". Tells apart printings with the same name, which
    /// include reprints in promo sets.
    /// </summary>
    public static string Meta(Card card) =>
        card.Variant is { } variant ? $"{Type(card)} · {variant} · {card.Set}" : $"{Type(card)} · {card.Set}";

    /// <summary>
    /// The card's domain, or a dash for cards without one.
    /// </summary>
    public static string Domain(Card card) => card.Domain is "" ? "—" : card.Domain;

    /// <summary>
    /// The card's energy cost, or a dash for cards without one (e.g. legends and battlefields).
    /// </summary>
    public static string Cost(Card card) => card.Energy?.ToString() ?? "—";
}
