using Riftcaster.Contracts;

namespace Riftcaster.Core.Timer;

/// <summary>
/// What <see cref="TimerService"/> stores: enough to work out the elapsed time at any moment,
/// without updating anything while the timer runs.
/// </summary>
/// <param name="TotalSeconds">The timer's length.</param>
/// <param name="ElapsedBeforeStart">
/// Time run before <paramref name="StartedAt"/>. While paused, this is all the time run.
/// </param>
/// <param name="StartedAt">When the timer was last started, or <see langword="null"/> while it's stopped.</param>
/// <param name="AllowOvertime">Whether the timer keeps counting past zero, from the match settings.</param>
public sealed record TimerClock(int TotalSeconds, TimeSpan ElapsedBeforeStart, DateTimeOffset? StartedAt, bool AllowOvertime)
{
    /// <summary>
    /// Whether the timer is counting.
    /// </summary>
    public bool Running => StartedAt is not null;

    /// <summary>
    /// The timer's length as a <see cref="TimeSpan"/>.
    /// </summary>
    public TimeSpan Total => TimeSpan.FromSeconds(TotalSeconds);

    /// <summary>
    /// The time run as of <paramref name="now"/>.
    /// </summary>
    public TimeSpan ElapsedAt(DateTimeOffset now) =>
        StartedAt is { } startedAt ? ElapsedBeforeStart + (now - startedAt) : ElapsedBeforeStart;

    /// <summary>
    /// Whether the timer has run out with overtime off, as of <paramref name="now"/>.
    /// It can't be started again until the total grows, or it's reset.
    /// </summary>
    public bool ExpiredAt(DateTimeOffset now) => !AllowOvertime && ElapsedAt(now) >= Total;

    /// <summary>
    /// The snapshot sent to the overlays, as of <paramref name="now"/>.
    /// </summary>
    public TimerState ToState(DateTimeOffset now) =>
        new(TotalSeconds, (long)ElapsedAt(now).TotalMilliseconds, Running, AllowOvertime);
}
