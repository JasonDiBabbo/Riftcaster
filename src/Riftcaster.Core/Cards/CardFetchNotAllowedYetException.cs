namespace Riftcaster.Core.Cards;

/// <summary>
/// A card source was asked for its cards while it has asked to be left alone (see
/// <see cref="ICardSource.NotBefore"/>), and the wait isn't over. Nothing was requested.
/// </summary>
/// <param name="notBefore">The earliest time the source may be asked.</param>
public sealed class CardFetchNotAllowedYetException(DateTimeOffset notBefore)
    : InvalidOperationException($"This card source asked to be left alone until {notBefore:u}.")
{
    /// <summary>
    /// The earliest time the source may be asked.
    /// </summary>
    public DateTimeOffset NotBefore { get; } = notBefore;
}
