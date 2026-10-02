using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Riftcaster.Contracts;

namespace Riftcaster.Core.Cards;

/// <summary>
/// Fetches the card catalogue from the Riftcodex API (https://riftcodex.com/docs), a page of
/// 100 cards at a time. A page that fails in a way that can clear up by itself (a dropped
/// connection, a timeout, an overloaded server) is tried again, so one bad moment doesn't cost the
/// whole fetch.
/// </summary>
/// <remarks>
/// Riftcodex can list one printing more than once: re-importing Vendetta left the old records of
/// 131 of its cards alongside the new ones, some with outdated names or no variant. Records with
/// the same collector code and image are the same printing, and any updated well before the newest
/// of them (see <see cref="StaleRecordAge"/>) are dropped. Genuinely different printings that share
/// both, like a promo and its metal version, were added together, so both are kept.
/// </remarks>
/// <param name="http">
/// A client whose base address is the API's (https://api.riftcodex.com/). It must send a
/// User-Agent header, without which the API refuses every request, and allow for slow pages:
/// one can take half a minute.
/// </param>
/// <param name="time">The clock, for the pause between attempts. Tests pass one that doesn't wait.</param>
/// <param name="logger">
/// Logs each page as it arrives (a whole fetch can take minutes), pages that are tried again,
/// cards skipped because they're missing something, and types or
/// supertypes this version doesn't know yet (those cards are kept, as <see cref="CardType.Other"/>
/// or <see cref="CardSupertype.Other"/>).
/// </param>
public sealed class RiftcodexCardSource(HttpClient http, TimeProvider time, ILogger<RiftcodexCardSource> logger) : ICardSource
{
    /// <summary>
    /// Cards per request: the API's largest page.
    /// </summary>
    public const int PageSize = 100;

    /// <summary>
    /// How many times a page is tried before the fetch gives up.
    /// </summary>
    public const int MaxAttempts = 3;

    /// <summary>
    /// The pause before the second attempt at a page; each later one waits this much longer again.
    /// </summary>
    public static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How much older than the newest record of the same printing a record must be to count as a
    /// leftover from an earlier import. Separate printings added together are seconds apart; the
    /// stale Vendetta records are days older.
    /// </summary>
    public static readonly TimeSpan StaleRecordAge = TimeSpan.FromHours(1);

    // Riftcodex's JSON names are snake_case (e.g. "image_url").
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Card>> FetchAllAsync(CancellationToken cancellationToken)
    {
        var fetched = new List<(Card Card, DateTimeOffset? UpdatedOn)>();
        var skipped = 0;
        var unknown = new SortedSet<string>();

        for (var page = 1; ; page++)
        {
            var result = await FetchPageAsync(page, cancellationToken);

            foreach (var item in result.Items ?? [])
            {
                if (ToCard(item, unknown) is { } card)
                {
                    fetched.Add((card, item?.Metadata?.UpdatedOn));
                }
                else
                {
                    skipped++;
                }
            }

            logger.LogInformation("Fetched card page {Page} of {Pages} ({Count} cards so far).", page, result.Pages, fetched.Count);

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

        // Drop records left over from an earlier import of the same printing (a missing date counts as oldest).
        var cards = fetched
            .GroupBy(entry => (entry.Card.Code, entry.Card.ImageUrl))
            .SelectMany(printing =>
            {
                var newest = printing.Max(entry => entry.UpdatedOn ?? DateTimeOffset.MinValue);
                return printing.Where(entry => newest - (entry.UpdatedOn ?? DateTimeOffset.MinValue) <= StaleRecordAge);
            })
            .Select(entry => entry.Card)
            .ToList();

        if (cards.Count < fetched.Count)
        {
            logger.LogInformation(
                "Dropped {Stale} Riftcodex records left over from an earlier import of the same printing.",
                fetched.Count - cards.Count);
        }

        return cards;
    }

    // One page, tried up to MaxAttempts times if it fails in a way that can clear up by itself.
    private async Task<CardPage> FetchPageAsync(int page, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await http.GetFromJsonAsync<CardPage>($"cards?page={page}&size={PageSize}", SerializerOptions, cancellationToken)
                    ?? throw new JsonException($"Riftcodex returned an empty response for page {page}.");
            }
            catch (Exception exception) when (attempt < MaxAttempts && IsTransient(exception, cancellationToken))
            {
                var delay = RetryDelay * attempt;
                logger.LogWarning(
                    exception,
                    "Riftcodex page {Page} failed (attempt {Attempt} of {MaxAttempts}); trying again in {Delay} seconds.",
                    page, attempt, MaxAttempts, delay.TotalSeconds);
                await Task.Delay(delay, time, cancellationToken);
            }
        }
    }

    // Worth trying again: no answer at all (refused or dropped), HttpClient's own timeout (which
    // surfaces as a cancellation nobody asked for), or an answer that means "not right now".
    // Anything else, such as a 403 for a missing User-Agent, would only fail the same way again.
    private static bool IsTransient(Exception exception, CancellationToken cancellationToken) => exception switch
    {
        HttpRequestException { StatusCode: null } => true,
        HttpRequestException { StatusCode: { } status } =>
            status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)status >= 500,
        TaskCanceledException => !cancellationToken.IsCancellationRequested,
        _ => false,
    };

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
        IReadOnlyList<string>? Tags,
        CardMetadata? Metadata);

    private sealed record CardClassification(string? Type, string? Supertype, IReadOnlyList<string>? Domain);

    private sealed record CardAttributes(int? Energy);

    private sealed record CardSet(string? Label);

    private sealed record CardMedia(string? ImageUrl);

    // When Riftcodex last changed the record ("updated_on" in the JSON).
    private sealed record CardMetadata(DateTimeOffset? UpdatedOn);
}
