using System.Text.Json.Serialization;

namespace Riftcaster.Contracts;

/// <summary>
/// What kind of card it is.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<CardType>))]
public enum CardType
{
    Unit,
    Spell,
    Legend,
    Gear,
    Battlefield,
    Rune,

    /// <summary>
    /// A type the card source has but this version doesn't know yet (e.g. from a new set).
    /// </summary>
    Other,
}
