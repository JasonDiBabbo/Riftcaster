namespace Riftcaster.Core.Cards;

/// <summary>
/// A card source was asked for its cards before it may be: its fetches are limited (see
/// <see cref="ICardSource.NotBefore"/>), and the wait isn't over. Nothing was requested.
/// </summary>
/// <param name="notBefore">The earliest time the source may be asked.</param>
public sealed class CardFetchNotAllowedYetException(DateTimeOffset notBefore)
    : InvalidOperationException($"This card source may not be asked again until {notBefore:u}.")
{
    /// <summary>
    /// The earliest time the source may be asked.
    /// </summary>
    public DateTimeOffset NotBefore { get; } = notBefore;
}
