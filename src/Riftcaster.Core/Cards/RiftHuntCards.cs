using System.Text.Json;
using Microsoft.Extensions.Logging;
using Riftcaster.Contracts;

namespace Riftcaster.Core.Cards;

/// <summary>
/// Reads RiftHunt's bulk card file (https://api.rifthunt.com/bulk/cards) into <see cref="Card"/>s.
/// </summary>
/// <remarks>
/// <para>
/// Not the shape its documentation describes: the file is one object with the cards under "cards", with camelCase
/// names (riftboundId, setName, imgUrl, ...), and names, types and supertypes spelled as Riftcodex's are, so those
/// are read the same way (see <see cref="RiftcodexNames"/> and <see cref="CardFieldMapping"/>).
/// </para>
/// <para>
/// A card is only listed if its image is card art: Riot's own (cmsassets.rgpub.io) or RiftHunt's. A few promos have
/// only a TCGplayer product photo, which isn't the card's shape and isn't ours to load from, so those are skipped.
/// </para>
/// </remarks>
internal static class RiftHuntCards
{
    // Where card art comes from. Anything else is a photo of something else.
    private static readonly string[] ArtHosts = ["cmsassets.rgpub.io", "rifthunt.com"];

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Reads the file.
    /// </summary>
    /// <param name="stream">The JSON.</param>
    /// <param name="logger">Logs cards that were skipped, and types this version doesn't know.</param>
    /// <param name="cancellationToken">Stops the reading.</param>
    /// <exception cref="JsonException">It isn't a card file.</exception>
    public static async Task<IReadOnlyList<Card>> ReadAsync(Stream stream, ILogger logger, CancellationToken cancellationToken)
    {
        var bulk = await JsonSerializer.DeserializeAsync<Bulk>(stream, SerializerOptions, cancellationToken);
        return Convert(bulk, logger);
    }

    /// <summary>
    /// Reads the file from text.
    /// </summary>
    /// <param name="json">The JSON.</param>
    /// <param name="logger">Logs cards that were skipped, and types this version doesn't know.</param>
    /// <exception cref="JsonException">It isn't a card file.</exception>
    public static IReadOnlyList<Card> Read(string json, ILogger logger) =>
        Convert(JsonSerializer.Deserialize<Bulk>(json, SerializerOptions), logger);

    private static IReadOnlyList<Card> Convert(Bulk? bulk, ILogger logger)
    {
        if (bulk?.Cards is not { } items)
        {
            throw new JsonException("RiftHunt's answer has no list of cards.");
        }

        var cards = new List<Card>(items.Count);
        var incomplete = 0;
        var notCardArt = 0;
        var unknown = new SortedSet<string>();

        foreach (var item in items)
        {
            switch (ToCard(item, unknown))
            {
                case (null, false):
                    incomplete++;
                    break;
                case (null, true):
                    notCardArt++;
                    break;
                case (var card, _):
                    cards.Add(card!);
                    break;
            }
        }

        if (incomplete > 0)
        {
            logger.LogWarning("Skipped {Skipped} RiftHunt cards that were missing a code, name, type, set or image.", incomplete);
        }

        if (notCardArt > 0)
        {
            logger.LogInformation("Skipped {Skipped} RiftHunt cards whose only image isn't the card's art.", notCardArt);
        }

        if (unknown.Count > 0)
        {
            logger.LogWarning("RiftHunt has card types or supertypes this version doesn't know, shown as Other: {Unknown}.", string.Join(", ", unknown));
        }

        return cards;
    }

    // The card, or null and why not: missing something (false), or its image isn't card art (true).
    private static (Card? Card, bool NotCardArt) ToCard(Item? item, ISet<string> unknown)
    {
        if (item is not { Id: { } id, RiftboundId: { } code, Name: { } name, Type: { } type, SetName: { } set, ImgUrl: { } imageUrl })
        {
            return (null, false);
        }

        if (!IsCardArt(imageUrl))
        {
            return (null, true);
        }

        var cardType = CardFieldMapping.ToType(type, unknown);
        var (displayName, variant) = RiftcodexNames.Normalize(name, cardType, item.Tags);

        return (new Card(
            id,
            code,
            displayName,
            variant,
            cardType,
            item.Supertype is { } supertype ? CardFieldMapping.ToSupertype(supertype, unknown) : null,
            string.Join(" / ", item.Domains ?? []),
            item.Energy,
            set,
            imageUrl,
            Landscape: item.Orientation == "landscape"), false);
    }

    private static bool IsCardArt(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && ArtHosts.Any(host => uri.Host == host || uri.Host.EndsWith("." + host, StringComparison.Ordinal));

    // The parts of RiftHunt's file this uses. Everything is nullable, so a card missing a field is read anyway and
    // then skipped by ToCard, rather than failing the whole file.
    private sealed record Bulk(IReadOnlyList<Item?>? Cards);

    // RiftboundId is the official collector code.
    private sealed record Item(
        string? Id,
        string? RiftboundId,
        string? Name,
        string? Type,
        string? Supertype,
        IReadOnlyList<string>? Domains,
        int? Energy,
        string? SetName,
        string? ImgUrl,
        string? Orientation,
        IReadOnlyList<string>? Tags);
}
