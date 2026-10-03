namespace Riftcaster.Contracts;

/// <summary>
/// The card on the Featured card overlay.
/// </summary>
/// <param name="Card">
/// The featured card, as it was when it was featured, or <see langword="null"/> when nothing is featured.
/// </param>
public record FeaturedCardState(Card? Card);
