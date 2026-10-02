namespace Riftcaster.Contracts;

/// <summary>
/// A Riftbound card, as the admin and the overlays show it.
/// </summary>
/// <remarks>
/// Describes the card itself, not where it came from: any card database (currently Riftcodex,
/// see RiftcodexCardSource) can supply these fields.
/// </remarks>
/// <param name="Id">
/// The card source's id for this printing: unique within that source, but meaningless outside
/// it, so treat it as opaque. Use <paramref name="Code"/> to recognise a card across sources.
/// </param>
/// <param name="Code">
/// The official collector code printed on the card, e.g. "ogn-247". The same in every card
/// database, but not unique: a card and its variants (e.g. a metal promo) can share one.
/// </param>
/// <param name="Name">
/// The card's name as shown on stream: "Champion, Title" for champion cards (e.g. "Kai'Sa,
/// Daughter of the Void"), without the variant.
/// </param>
/// <param name="Variant">
/// Which printing this is, e.g. "Alternate Art", "Signature" or "Metal", or <see langword="null"/>
/// for the standard one. Tells apart cards with the same name in the admin.
/// </param>
/// <param name="Type">What kind of card it is.</param>
/// <param name="Supertype">The card's supertype, or <see langword="null"/> for none.</param>
/// <param name="Domain">The card's domain, or both joined with " / " (e.g. "Fury / Order").</param>
/// <param name="Energy">The energy cost, or <see langword="null"/> for cards without one (e.g. legends and battlefields).</param>
/// <param name="Set">The set's name, e.g. "Origins".</param>
/// <param name="ImageUrl">The address of the card's image.</param>
/// <param name="Landscape">Whether the image is landscape (battlefields) rather than portrait.</param>
public record Card(
    string Id,
    string Code,
    string Name,
    string? Variant,
    CardType Type,
    CardSupertype? Supertype,
    string Domain,
    int? Energy,
    string Set,
    string ImageUrl,
    bool Landscape);
