using Riftcaster.Contracts;

namespace Riftcaster.Core.Cards;

/// <summary>
/// The whole card catalogue as fetched at one moment: what the card store saves.
/// </summary>
/// <param name="Cards">Every card, in the source's order.</param>
/// <param name="FetchedAt">When the cards were fetched.</param>
public sealed record CardCatalogSnapshot(IReadOnlyList<Card> Cards, DateTimeOffset FetchedAt);
