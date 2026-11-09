using System.Net;
using Microsoft.Extensions.Logging;
using Riftcaster.Contracts;

namespace Riftcaster.Core.Cards;

/// <summary>
/// Fetches the card catalog from RiftHunt (https://rifthunt.com/api/): the whole of it in one download, which
/// RiftHunt asks users not to make more than about once a day.
/// </summary>
/// <remarks>
/// <para>
/// RiftHunt has no hard limit but a request, so keeping to it is left to whoever asks: the Settings page asks the
/// person for confirmation before a fetch, and the background refresher fetches once a day and backs off after a
/// failure (CardCatalogRefresher, in the server). This source enforces one thing: when RiftHunt itself
/// says how long to wait (a 429 or 503 with Retry-After), it isn't asked before then, by anyone
/// (<see cref="NotBefore"/>, <see cref="CardFetchNotAllowedYetException"/>). That is remembered in memory only, so
/// restarting Riftcaster forgets it.
/// </para>
/// <para>Each request says who is asking (the User-Agent of <paramref name="http"/>), which RiftHunt asks for.</para>
/// </remarks>
/// <param name="http">
/// A client whose base address is the API's (https://api.rifthunt.com/), sending a User-Agent that names
/// Riftcaster. Tests pass one whose handler throws unless the test supplies an answer.
/// </param>
/// <param name="time">The clock.</param>
/// <param name="logger">Logs each fetch and why one failed.</param>
public sealed class RiftHuntCardSource(HttpClient http, TimeProvider time, ILogger<RiftHuntCardSource> logger) : ICardSource
{
    /// <summary>
    /// The address of the bulk card file, relative to the API's.
    /// </summary>
    public const string BulkPath = "bulk/cards";

    /// <summary>
    /// The longest wait a Retry-After is believed: a number of days is more likely a mistake than a request.
    /// </summary>
    public static readonly TimeSpan LongestRetryAfter = TimeSpan.FromDays(7);

    private readonly Lock _lock = new();

    private DateTimeOffset? _askedToWaitUntil;

    /// <inheritdoc/>
    public DateTimeOffset? NotBefore
    {
        get
        {
            lock (_lock)
            {
                return _askedToWaitUntil is { } until && until > time.GetUtcNow() ? until : null;
            }
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Card>> FetchAllAsync(IProgress<CardFetchProgress>? progress, CancellationToken cancellationToken)
    {
        if (NotBefore is { } notBefore)
        {
            throw new CardFetchNotAllowedYetException(notBefore);
        }

        logger.LogInformation("Fetching the RiftHunt card catalog.");
        using var response = await http.GetAsync(BulkPath, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            AskedToWait(response.StatusCode, response.Headers.RetryAfter);
            throw new HttpRequestException($"RiftHunt answered {(int)response.StatusCode} {response.ReasonPhrase}.", inner: null, response.StatusCode);
        }

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var cards = await RiftHuntCards.ReadAsync(stream, logger, cancellationToken);
            logger.LogInformation("Fetched {Count} cards from RiftHunt.", cards.Count);
            return cards;
        }
        catch (IOException exception)
        {
            // The download was cut off part way.
            throw new HttpRequestException("The RiftHunt download was interrupted.", exception);
        }
    }

    // Remembers that RiftHunt said when to ask again, if it did: a 429 or 503 with Retry-After.
    private void AskedToWait(HttpStatusCode status, System.Net.Http.Headers.RetryConditionHeaderValue? retryAfter)
    {
        if (status is not (HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable))
        {
            return;
        }

        var asked = retryAfter switch
        {
            { Delta: { } delta } => delta,
            { Date: { } date } => date - time.GetUtcNow(),
            _ => TimeSpan.Zero,
        };

        if (asked <= TimeSpan.Zero)
        {
            return;
        }

        var wait = TimeSpan.FromTicks(Math.Min(asked.Ticks, LongestRetryAfter.Ticks));
        lock (_lock)
        {
            _askedToWaitUntil = time.GetUtcNow() + wait;
        }

        logger.LogWarning("RiftHunt asked Riftcaster to wait {Minutes:N0} minutes before asking again, and it won't be asked before then.", wait.TotalMinutes);
    }
}
