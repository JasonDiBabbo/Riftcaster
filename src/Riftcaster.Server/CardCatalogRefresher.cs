using Riftcaster.Core.Cards;

namespace Riftcaster.Server;

/// <summary>
/// Keeps the card catalog current while the server runs: fetches it when it's due, then once a
/// day, so new sets appear by themselves. A failed fetch is retried sooner.
/// </summary>
/// <remarks>
/// Runs in the background, so the server starts at once: the whole catalog can take minutes to
/// fetch, and until then the catalog has its last saved cards.
/// </remarks>
/// <param name="catalog">The catalog to refresh.</param>
/// <param name="time">The clock. Tests pass a fake one they can move forward.</param>
/// <param name="logger">Logs each fetch.</param>
public sealed class CardCatalogRefresher(CardCatalog catalog, TimeProvider time, ILogger<CardCatalogRefresher> logger) : BackgroundService
{
    /// <summary>
    /// How long a fetched catalog is kept before fetching again.
    /// </summary>
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromDays(1);

    /// <summary>
    /// How long to wait after a failed fetch (e.g. no internet) before trying again.
    /// </summary>
    public static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(15);

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // A catalog fetched recently, e.g. before a restart, is good until it's due.
            await DelayAsync(DelayBeforeFirstFetch(catalog.FetchedAt, time.GetUtcNow()), stoppingToken);

            while (true)
            {
                logger.LogInformation("Fetching the card catalog; this can take a few minutes.");
                var refreshed = await catalog.RefreshAsync(stoppingToken);
                if (refreshed)
                {
                    logger.LogInformation("Card catalog fetched: {Count} cards.", catalog.Cards.Count);
                }

                await DelayAsync(DelayAfterFetch(refreshed), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The server is shutting down.
        }
    }

    /// <summary>
    /// How long to wait before the first fetch: until a day after the last one, or not at all if
    /// the catalog has never been fetched or is already overdue.
    /// </summary>
    /// <param name="fetchedAt">When the catalog was last fetched, if ever.</param>
    /// <param name="now">The time now.</param>
    public static TimeSpan DelayBeforeFirstFetch(DateTimeOffset? fetchedAt, DateTimeOffset now) =>
        fetchedAt is { } fetched && fetched + RefreshInterval > now ? fetched + RefreshInterval - now : TimeSpan.Zero;

    /// <summary>
    /// How long to wait before the next fetch: a day after one that worked, less after one that didn't.
    /// </summary>
    /// <param name="refreshed">Whether the fetch brought a new catalog.</param>
    public static TimeSpan DelayAfterFetch(bool refreshed) => refreshed ? RefreshInterval : RetryInterval;

    private Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        delay > TimeSpan.Zero ? Task.Delay(delay, time, cancellationToken) : Task.CompletedTask;
}
