using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Riftcaster.Contracts;
using Riftcaster.Core.Match;

namespace Riftcaster.Core.Timer;

/// <summary>
/// The service for the match timer: start, pause, reset and adjust it, and stop it at zero
/// when overtime is off.
/// </summary>
/// <remarks>
/// Nothing changes while the timer runs: it stores when it started, and the elapsed time is worked
/// out when asked for. So <see cref="Changed"/> is raised only when the operator (or reaching zero)
/// changes something, never once per tick.
/// </remarks>
public sealed class TimerService : IDisposable
{
    /// <summary>
    /// The longest allowed total: 999:59, the most "Set remaining" accepts.
    /// </summary>
    public const int MaxTotalSeconds = TimerFormat.MaxMinutes * 60 + 59;

    private readonly MatchService _match;

    private readonly TimeProvider _time;

    private readonly Lock _lock = new();

    private TimerClock _clock;

    // Fires when the timer reaches zero, if it's running with overtime off. Replaced on every change.
    private ITimer? _expiry;

    /// <summary>
    /// Creates the service, ready to start at the match duration.
    /// </summary>
    /// <param name="match">The match settings: the duration and whether overtime is allowed.</param>
    /// <param name="time">The clock. Tests pass a fake one they can move forward.</param>
    public TimerService(MatchService match, TimeProvider time)
    {
        _match = match;
        _time = time;
        _clock = new TimerClock(match.Settings.DurationMinutes * 60, TimeSpan.Zero, null, match.Settings.AllowOvertime);

        // Follow the duration while the timer is untouched, and pick up the overtime setting.
        // Both services live as long as the app, so this handler never needs removing.
        match.Changed += () => Apply((clock, now) => IsUntouched(clock)
            ? clock with { TotalSeconds = _match.Settings.DurationMinutes * 60 }
            : clock);
    }

    /// <summary>
    /// The timer as of now.
    /// </summary>
    public TimerState State => _clock.ToState(_time.GetUtcNow());

    /// <summary>
    /// Raised after the timer is started, paused, reset, adjusted or set, or stops at zero.
    /// Handlers read <see cref="State"/> for the new values.
    /// </summary>
    /// <remarks>
    /// May be raised on any thread.
    /// </remarks>
    public event Action? Changed;

    /// <summary>
    /// Starts or resumes the timer. Does nothing if it's running, or if it has run out with overtime off.
    /// </summary>
    public void Start() => Apply((clock, now) =>
        clock.Running || clock.ExpiredAt(now) ? clock : clock with { StartedAt = now });

    /// <summary>
    /// Pauses the timer, keeping the time run so far. Does nothing if it isn't running.
    /// </summary>
    public void Pause() => Apply((clock, now) =>
        clock.Running ? clock with { ElapsedBeforeStart = clock.ElapsedAt(now), StartedAt = null } : clock);

    /// <summary>
    /// Stops the timer, clears the time run, and sets the total to the match duration.
    /// </summary>
    public void Reset() => Apply((clock, now) =>
        clock with { TotalSeconds = _match.Settings.DurationMinutes * 60, ElapsedBeforeStart = TimeSpan.Zero, StartedAt = null });

    /// <summary>
    /// Adds to or takes from the total, for example <c>Adjust(-60)</c> for −1m. The time run is unchanged.
    /// The total stays between 0 and <see cref="MaxTotalSeconds"/>.
    /// </summary>
    /// <param name="seconds">The seconds to add, or take away if negative.</param>
    public void Adjust(int seconds) => Apply((clock, now) =>
        clock with { TotalSeconds = (int)Math.Clamp((long)clock.TotalSeconds + seconds, 0, MaxTotalSeconds) });

    /// <summary>
    /// Sets the time remaining: the total becomes <paramref name="seconds"/> and the time run is
    /// cleared. A running timer keeps running, from now.
    /// </summary>
    /// <param name="seconds">The time remaining, from 0 to <see cref="MaxTotalSeconds"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seconds"/> is out of range.</exception>
    public void SetRemaining(int seconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(seconds);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(seconds, MaxTotalSeconds);

        Apply((clock, now) => clock with
        {
            TotalSeconds = seconds,
            ElapsedBeforeStart = TimeSpan.Zero,
            StartedAt = clock.Running ? now : null,
        });
    }

    /// <summary>
    /// Streams the timer: first its current state, then a new snapshot after each change.
    /// </summary>
    /// <remarks>
    /// If changes arrive faster than the caller reads them, only the latest is sent. Each snapshot
    /// is taken as it's sent, so its elapsed time is current.
    /// </remarks>
    /// <param name="cancellationToken">Ends the stream, normally rather than with an exception.</param>
    /// <returns>The current state, then each new state.</returns>
    public async IAsyncEnumerable<TimerState> WatchAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // A signal only: the reader looks up the current clock itself, so it always sends the latest.
        var changed = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });

        void OnChanged()
        {
            changed.Writer.TryWrite(true);
        }

        using var registration = cancellationToken.Register(() => changed.Writer.TryComplete());
        Changed += OnChanged;

        try
        {
            var last = _clock;
            yield return last.ToState(_time.GetUtcNow()); // Initial state

            // Not cancellationToken: cancellation completes the channel (above), which ends this loop
            // normally. Passing the token would end it with an OperationCanceledException instead.
            await foreach (var _ in changed.Reader.ReadAllAsync(CancellationToken.None))
            {
                // Compare clocks, not snapshots: a running timer's snapshots always differ.
                var current = _clock;
                if (current == last)
                {
                    continue; // Already sent: it was read after an earlier signal
                }

                last = current;
                yield return current.ToState(_time.GetUtcNow());
            }
        }
        finally
        {
            Changed -= OnChanged;
        }
    }

    /// <summary>
    /// Stops the zero timer.
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            _expiry?.Dispose();
            _expiry = null;
        }
    }

    // Not running and never started since the last reset or set: safe to follow the match duration.
    private static bool IsUntouched(TimerClock clock) =>
        !clock.Running && clock.ElapsedBeforeStart == TimeSpan.Zero;

    private void Apply(Func<TimerClock, DateTimeOffset, TimerClock> change)
    {
        bool changed;

        lock (_lock)
        {
            var now = _time.GetUtcNow();
            var updated = change(_clock, now) with { AllowOvertime = _match.Settings.AllowOvertime };

            // Without overtime, a running timer stops at zero: reached by running, by a smaller
            // total, or by overtime being turned off.
            if (updated.Running && updated.ExpiredAt(now))
            {
                updated = updated with { ElapsedBeforeStart = updated.Total, StartedAt = null };
            }

            changed = updated != _clock;
            _clock = updated;

            // Every time, even without a change: if the zero timer fired a moment early, this
            // schedules it again for the moment that's left.
            ScheduleExpiry(now);
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    // Must be called inside the lock.
    private void ScheduleExpiry(DateTimeOffset now)
    {
        _expiry?.Dispose();
        _expiry = null;

        if (_clock.Running && !_clock.AllowOvertime)
        {
            var remaining = _clock.Total - _clock.ElapsedAt(now);
            _expiry = _time.CreateTimer(_ => Apply((clock, at) => clock), null, remaining, Timeout.InfiniteTimeSpan);
        }
    }
}
