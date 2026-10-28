using Riftcaster.Contracts;

namespace Riftcaster.Core.Cards;

/// <summary>
/// How card databases name types and supertypes, mapped to ours. Shared by the sources, which spell them alike.
/// </summary>
/// <remarks>
/// Mapped explicitly rather than parsed, so ours don't have to match a database's spelling. One a source has
/// but this version doesn't know is added to <c>unknown</c> and read as Other: the card is kept.
/// </remarks>
internal static class CardFieldMapping
{
    public static CardType ToType(string type, ISet<string> unknown)
    {
        switch (type)
        {
            case "Unit": return CardType.Unit;
            case "Spell": return CardType.Spell;
            case "Legend": return CardType.Legend;
            case "Gear": return CardType.Gear;
            case "Battlefield": return CardType.Battlefield;
            case "Rune": return CardType.Rune;
            default:
                unknown.Add(type);
                return CardType.Other;
        }
    }

    public static CardSupertype ToSupertype(string supertype, ISet<string> unknown)
    {
        switch (supertype)
        {
            case "Champion": return CardSupertype.Champion;
            case "Signature": return CardSupertype.Signature;
            case "Basic": return CardSupertype.Basic;
            case "Token": return CardSupertype.Token;
            default:
                unknown.Add(supertype);
                return CardSupertype.Other;
        }
    }
}
