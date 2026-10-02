using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Riftcaster.Contracts;

namespace Riftcaster.Core.Cards;

/// <summary>
/// Fetches the card catalogue from the Riftcodex API (https://riftcodex.com/docs), a page of
/// 100 cards at a time.
/// </summary>
/// <param name="http">
/// A client whose base address is the API's (https://api.riftcodex.com/). It must send a
/// User-Agent header, without which the API refuses every request, and allow for slow pages:
/// one can take half a minute.
/// </param>
/// <param name="logger">
/// Logs cards skipped because they're missing something, and types or supertypes this version
/// doesn't know yet (those cards are kept, as <see cref="CardType.Other"/> or <see cref="CardSupertype.Other"/>).
/// </param>
public sealed class RiftcodexCardSource(HttpClient http, ILogger<RiftcodexCardSource> logger) : ICardSource
{
    /// <summary>
    /// Cards per request: the API's largest page.
    /// </summary>
    public const int PageSize = 100;

    // Riftcodex's JSON names are snake_case (e.g. "image_url").
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Card>> FetchAllAsync(CancellationToken cancellationToken)
    {
        var cards = new List<Card>();
        var skipped = 0;
        var unknown = new SortedSet<string>();

        for (var page = 1; ; page++)
        {
            var result = await http.GetFromJsonAsync<CardPage>($"cards?page={page}&size={PageSize}", SerializerOptions, cancellationToken)
                ?? throw new JsonException($"Riftcodex returned an empty response for page {page}.");

            foreach (var item in result.Items ?? [])
            {
                if (ToCard(item, unknown) is { } card)
                {
                    cards.Add(card);
                }
                else
                {
                    skipped++;
                }
            }

            if (page >= result.Pages)
            {
                break;
            }
        }

        if (skipped > 0)
        {
            logger.LogWarning("Skipped {Skipped} Riftcodex cards that were missing a code, name, type, set or image.", skipped);
        }

        if (unknown.Count > 0)
        {
            logger.LogWarning("Riftcodex has card types or supertypes this version doesn't know, shown as Other: {Unknown}.", string.Join(", ", unknown));
        }

        return cards;
    }

    // Null for a card missing anything the admin or overlays need: one malformed card is skipped
    // rather than failing the whole catalogue. Adds any type or supertype it doesn't know to unknown.
    private static Card? ToCard(RiftcodexCard? item, ISet<string> unknown)
    {
        if (item is not
            {
                Id: { } id,
                RiftboundId: { } code,
                Name: { } name,
                Classification.Type: { } type,
                Set.Label: { } set,
                Media.ImageUrl: { } imageUrl,
            })
        {
            return null;
        }

        var cardType = ToType(type, unknown);
        var (displayName, variant) = RiftcodexNames.Normalize(name, cardType, item.Tags);

        return new Card(
            id,
            code,
            displayName,
            variant,
            cardType,
            item.Classification.Supertype is { } supertype ? ToSupertype(supertype, unknown) : null,
            string.Join(" / ", item.Classification.Domain ?? []),
            item.Attributes?.Energy,
            set,
            imageUrl,
            Landscape: item.Orientation == "landscape");
    }

    // Riftcodex's names for types and supertypes, mapped explicitly rather than parsed, so ours don't
    // have to match its spelling.
    private static CardType ToType(string type, ISet<string> unknown)
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

    private static CardSupertype ToSupertype(string supertype, ISet<string> unknown)
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

    // The parts of Riftcodex's response this uses. Everything is nullable, so a card missing a
    // field is read anyway and then skipped by ToCard, rather than failing the whole page.
    private sealed record CardPage(IReadOnlyList<RiftcodexCard?>? Items, int Pages);

    // RiftboundId is the official collector code ("riftbound_id" in the JSON).
    private sealed record RiftcodexCard(
        string? Id,
        string? RiftboundId,
        string? Name,
        CardClassification? Classification,
        CardAttributes? Attributes,
        CardSet? Set,
        CardMedia? Media,
        string? Orientation,
        IReadOnlyList<string>? Tags);

    private sealed record CardClassification(string? Type, string? Supertype, IReadOnlyList<string>? Domain);

    private sealed record CardAttributes(int? Energy);

    private sealed record CardSet(string? Label);

    private sealed record CardMedia(string? ImageUrl);
}
