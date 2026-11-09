using Riftcaster.Core.Cards;

namespace Riftcaster.Server;

/// <summary>
/// Keeps the card catalog current while the server runs: fetches it when it's due, then once a
/// day, so new sets appear by themselves. A failed fetch is tried again after a wait that depends on why it failed
/// (<see cref="DelayAfterFailure"/>).
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
    /// How long to wait after a fetch that never got an answer (e.g. no internet) before trying again.
    /// </summary>
    public static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(15);

    /// <summary>
    /// The wait after a source first answers that it can't serve the request; each failure in a row doubles it.
    /// </summary>
    public static readonly TimeSpan FirstBackoff = TimeSpan.FromHours(1);

    /// <summary>
    /// The longest a failure makes the refresher wait, however many there have been.
    /// </summary>
    public static readonly TimeSpan LongestBackoff = TimeSpan.FromDays(1);

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Switching to another card source starts the wait again: its catalog has its own age, and its source its own limits.
        var switched = new SemaphoreSlim(0);
        void OnSourceChanged() => switched.Release();
        catalog.SourceChanged += OnSourceChanged;

        try
        {
            // A catalog fetched recently, e.g. before a restart, is good until it's due.
            var delay = DelayBeforeFirstFetch(catalog.FetchedAt, time.GetUtcNow(), catalog.NotBefore);

            // How many fetches in a row the source answered that it couldn't serve. Kept in memory: a restart starts over.
            var serverErrors = 0;

            while (true)
            {
                if (await WaitAsync(delay, switched, stoppingToken))
                {
                    delay = DelayBeforeFirstFetch(catalog.FetchedAt, time.GetUtcNow(), catalog.NotBefore);
                    serverErrors = 0;
                    continue;
                }

                logger.LogInformation("Fetching the card catalog; this can take a few minutes.");
                var failedBefore = catalog.Status.LastFailure;
                var refreshed = await catalog.RefreshAsync(stoppingToken);
                if (refreshed)
                {
                    logger.LogInformation("Card catalog fetched: {Count} cards.", catalog.Cards.Count);
                    serverErrors = 0;
                }

                // A failure of this fetch, not one left from before: each is a new record.
                var failure = catalog.Status.LastFailure is { } latest && !ReferenceEquals(latest, failedBefore) ? latest : null;
                serverErrors = failure?.Kind == CardFailureKind.ServerError ? serverErrors + 1 : failure is null ? serverErrors : 0;
                var after = failure is null ? DelayAfterFetch(refreshed) : DelayAfterFailure(failure.Kind, serverErrors);
                delay = Longest(after, catalog.NotBefore, time.GetUtcNow());
                if (failure is not null)
                {
                    logger.LogInformation("Trying the card catalog again in {Minutes:N0} minutes.", delay.TotalMinutes);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The server is shutting down.
        }
        finally
        {
            catalog.SourceChanged -= OnSourceChanged;
        }
    }

    /// <summary>
    /// How long to wait before the first fetch: until a day after the last one, or not at all if
    /// the catalog has never been fetched or is already overdue. Never before its source allows.
    /// </summary>
    /// <param name="fetchedAt">When the catalog was last fetched, if ever.</param>
    /// <param name="now">The time now.</param>
    /// <param name="notBefore">The earliest time the source may be asked, if it limits that.</param>
    public static TimeSpan DelayBeforeFirstFetch(DateTimeOffset? fetchedAt, DateTimeOffset now, DateTimeOffset? notBefore = null)
    {
        var due = fetchedAt is { } fetched && fetched + RefreshInterval > now ? fetched + RefreshInterval - now : TimeSpan.Zero;
        return Longest(due, notBefore, now);
    }

    /// <summary>
    /// How long to wait before the next fetch: a day after one that worked, less after one that didn't.
    /// </summary>
    /// <param name="refreshed">Whether the fetch brought a new catalog.</param>
    public static TimeSpan DelayAfterFetch(bool refreshed) => refreshed ? RefreshInterval : RetryInterval;

    /// <summary>
    /// How long to wait before the next fetch after one failed: soon if it never got an answer, which costs the source
    /// nothing, but an hour (doubling with each failure in a row, to a day) after the source said it couldn't serve the
    /// request, and a day after an answer that would be the same again.
    /// </summary>
    /// <param name="kind">Why it failed.</param>
    /// <param name="serverErrors">How many fetches in a row the source answered that it couldn't serve, this one included.</param>
    public static TimeSpan DelayAfterFailure(CardFailureKind kind, int serverErrors) => kind switch
    {
        CardFailureKind.NotReached => RetryInterval,
        CardFailureKind.ServerError => TimeSpan.FromTicks(Math.Min(FirstBackoff.Ticks << Math.Clamp(serverErrors - 1, 0, 5), LongestBackoff.Ticks)),
        _ => RefreshInterval,
    };

    /// <summary>
    /// How long to wait before the next fetch, as <see cref="DelayAfterFetch(bool)"/> but never before the source allows.
    /// </summary>
    /// <param name="refreshed">Whether the fetch brought a new catalog.</param>
    /// <param name="notBefore">The earliest time the source may be asked, if it limits that.</param>
    /// <param name="now">The time now.</param>
    public static TimeSpan DelayAfterFetch(bool refreshed, DateTimeOffset? notBefore, DateTimeOffset now) =>
        Longest(DelayAfterFetch(refreshed), notBefore, now);

    private static TimeSpan Longest(TimeSpan delay, DateTimeOffset? notBefore, DateTimeOffset now) =>
        notBefore is { } allowed && allowed - now > delay ? allowed - now : delay;

    // Waits for the delay, or until a switch of source. Returns whether it was cut short by one.
    private async Task<bool> WaitAsync(TimeSpan delay, SemaphoreSlim switched, CancellationToken cancellationToken)
    {
        if (delay <= TimeSpan.Zero)
        {
            return switched.CurrentCount > 0 && await switched.WaitAsync(TimeSpan.Zero, cancellationToken);
        }

        using var sleep = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var switchedTask = switched.WaitAsync(sleep.Token);
        var sleepTask = Task.Delay(delay, time, sleep.Token);
        var first = await Task.WhenAny(switchedTask, sleepTask);
        await sleep.CancelAsync();

        if (first == switchedTask)
        {
            await switchedTask; // Observed: it completed
            return true;
        }

        // Quiet whichever is left over.
        try
        {
            await switchedTask;
        }
        catch (OperationCanceledException)
        {
            // Canceled with the sleep
        }

        await sleepTask;
        return false;
    }
}
