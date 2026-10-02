using System.Text.Json.Serialization;

namespace Riftcaster.Contracts;

/// <summary>
/// A card's supertype, for the cards that have one.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<CardSupertype>))]
public enum CardSupertype
{
    Champion,
    Signature,
    Basic,
    Token,

    /// <summary>
    /// A supertype the card source has but this version doesn't know yet (e.g. from a new set).
    /// </summary>
    Other,
}
