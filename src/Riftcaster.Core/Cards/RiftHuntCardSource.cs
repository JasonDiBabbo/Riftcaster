using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Riftcaster.Contracts;

namespace Riftcaster.Core.Cards;

/// <summary>
/// Fetches the card catalog from RiftHunt (https://rifthunt.com/api/): the whole of it in one download, which
/// RiftHunt asks to be fetched at most once a day.
/// </summary>
/// <remarks>
/// <para>
/// RiftHunt has no hard limit but a request, and the rules here keep to it more strictly than a page that asks
/// for little would need, since being blocked would cost the catalog:
/// </para>
/// <list type="bullet">
/// <item>A day (<see cref="Cooldown"/>) passes after a fetch before the next, kept in <see cref="ICardFetchHistory"/>
/// so restarting Riftcaster doesn't skip it, and enforced here, not left to whoever asks
/// (<see cref="NotBefore"/>, <see cref="CardFetchNotAllowedYetException"/>).</item>
/// <item>The attempt is noted before the request goes out, so a crash during it still counts.</item>
/// <item>A failed fetch is never tried again at once. After a server error or no answer, the wait starts at an hour
/// (<see cref="FirstBackoff"/>) and doubles with each failure in a row, to a day. A 429 or 503 that says how long
/// to wait (Retry-After) is waited for if that's longer.</item>
/// <item>A request RiftHunt refuses (a 4xx other than 429), or an answer that isn't a card file, would only fail
/// the same way again, so it waits a full day.</item>
/// <item>Each request says who is asking (the User-Agent of <paramref name="http"/>), which RiftHunt asks for.</item>
/// </list>
/// </remarks>
/// <param name="http">
/// A client whose base address is the API's (https://api.rifthunt.com/), sending a User-Agent that names
/// Riftcaster. Tests pass one whose handler throws unless the test supplies an answer.
/// </param>
/// <param name="history">Remembers when the source may next be asked.</param>
/// <param name="time">The clock.</param>
/// <param name="logger">Logs each fetch and why one failed.</param>
public sealed class RiftHuntCardSource(HttpClient http, ICardFetchHistory history, TimeProvider time, ILogger<RiftHuntCardSource> logger) : ICardSource
{
    /// <summary>
    /// The address of the bulk card file, relative to the API's.
    /// </summary>
    public const string BulkPath = "bulk/cards";

    /// <summary>
    /// How long after a fetch the next may be made.
    /// </summary>
    public static readonly TimeSpan Cooldown = TimeSpan.FromDays(1);

    /// <summary>
    /// The wait after the first failure; each failure in a row doubles it.
    /// </summary>
    public static readonly TimeSpan FirstBackoff = TimeSpan.FromHours(1);

    /// <summary>
    /// The longest a failure makes the source wait, however many there have been.
    /// </summary>
    public static readonly TimeSpan LongestBackoff = TimeSpan.FromDays(1);

    /// <summary>
    /// The longest wait a Retry-After is believed: a number of days is more likely a mistake than a request.
    /// </summary>
    public static readonly TimeSpan LongestRetryAfter = TimeSpan.FromDays(7);

    /// <inheritdoc/>
    public DateTimeOffset? NotBefore => history.Get(CardSourceKind.RiftHunt).NotBefore is { } notBefore && notBefore > time.GetUtcNow() ? notBefore : null;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Card>> FetchAllAsync(IProgress<CardFetchProgress>? progress, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var before = history.Get(CardSourceKind.RiftHunt);
        if (before.NotBefore is { } notBefore && notBefore > now)
        {
            throw new CardFetchNotAllowedYetException(notBefore);
        }

        // Noted before the request, with the shortest wait there is, so that a crash or a stopped server
        // during it can't leave the source free to be asked again at once.
        history.Save(CardSourceKind.RiftHunt, new CardFetchRecord(now, now + FirstBackoff, before.Failures));

        try
        {
            logger.LogInformation("Fetching the RiftHunt card catalog.");
            using var response = await http.GetAsync(BulkPath, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var wait = WaitAfter(response.StatusCode, before.Failures + 1, response.Headers.RetryAfter);
                Failed(before, wait);
                throw new HttpRequestException($"RiftHunt answered {(int)response.StatusCode} {response.ReasonPhrase}.", inner: null, response.StatusCode);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var cards = await RiftHuntCards.ReadAsync(stream, logger, cancellationToken);
            history.Save(CardSourceKind.RiftHunt, new CardFetchRecord(now, time.GetUtcNow() + Cooldown, Failures: 0));
            logger.LogInformation("Fetched {Count} cards from RiftHunt; it won't be asked again for {Hours} hours.", cards.Count, Cooldown.TotalHours);
            return cards;
        }
        catch (HttpRequestException exception) when (exception.StatusCode is null)
        {
            Failed(before, Backoff(before.Failures + 1)); // Never answered: refused, dropped or timed out
            throw;
        }
        catch (IOException exception)
        {
            // The download was cut off part way.
            Failed(before, Backoff(before.Failures + 1));
            throw new HttpRequestException("The RiftHunt download was interrupted.", exception);
        }
        catch (JsonException)
        {
            Failed(before, Cooldown); // Answered with something that isn't the card file: the same again, until it's fixed
            throw;
        }
        catch (OperationCanceledException)
        {
            // Stopped, or a timeout: the request may have gone out, so it counts as an attempt, not as a failure
            // to wait a day for.
            Failed(before, FirstBackoff);
            throw;
        }
    }

    // Remembers a failed attempt and when to try again.
    private void Failed(CardFetchRecord before, TimeSpan wait)
    {
        var now = time.GetUtcNow();
        history.Save(CardSourceKind.RiftHunt, new CardFetchRecord(now, now + wait, before.Failures + 1));
        logger.LogWarning("The RiftHunt fetch failed; RiftHunt won't be asked again for {Minutes:N0} minutes.", wait.TotalMinutes);
    }

    // How long to wait after RiftHunt refused or couldn't serve a request.
    private TimeSpan WaitAfter(HttpStatusCode status, int failures, System.Net.Http.Headers.RetryConditionHeaderValue? retryAfter)
    {
        // Refused for what was asked: the same request would be refused again.
        if ((int)status is >= 400 and < 500 && status != HttpStatusCode.TooManyRequests && status != HttpStatusCode.RequestTimeout)
        {
            return Cooldown;
        }

        // 429, 503 and the other 5xx: later, and as much later as RiftHunt says if it says.
        var wait = Backoff(failures);
        var asked = retryAfter switch
        {
            { Delta: { } delta } => delta,
            { Date: { } date } => date - time.GetUtcNow(),
            _ => TimeSpan.Zero,
        };

        return asked > wait ? TimeSpan.FromTicks(Math.Min(asked.Ticks, LongestRetryAfter.Ticks)) : wait;
    }

    // An hour after the first failure, doubling, to a day.
    private static TimeSpan Backoff(int failures)
    {
        var doublings = Math.Min(failures - 1, 5);
        return TimeSpan.FromTicks(Math.Min(FirstBackoff.Ticks << doublings, LongestBackoff.Ticks));
    }
}
