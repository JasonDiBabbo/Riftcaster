using Riftcaster.Contracts;

namespace Riftcaster.Core.Timer;

/// <summary>
/// What the timer shows for a snapshot: the status, the big time and how far along it is.
/// </summary>
/// <param name="Status">The status beside the panel's title.</param>
/// <param name="Tone">The time's colour.</param>
/// <param name="Time">The time remaining as <c>mm:ss</c>, or the time past zero as <c>+mm:ss</c>.</param>
/// <param name="Progress">The fraction of the total that has run, from 0 to 1.</param>
public sealed record TimerReadout(TimerStatus Status, TimerTone Tone, string Time, double Progress)
{
    /// <summary>
    /// Below this much time left, a started timer is shown in the low-time colour: 5 minutes.
    /// </summary>
    public const int LowTimeSeconds = 5 * 60;

    /// <summary>
    /// Works out what to show for a snapshot.
    /// </summary>
    /// <param name="state">The timer, as of the moment to show.</param>
    /// <returns>What to show.</returns>
    public static TimerReadout From(TimerState state)
    {
        var remainingMilliseconds = state.TotalSeconds * 1000L - state.ElapsedMilliseconds;
        var started = state.ElapsedMilliseconds > 0;
        var overtime = remainingMilliseconds < 0 && state.AllowOvertime;

        var status = overtime ? (state.Running ? TimerStatus.Overtime : TimerStatus.OvertimePaused)
            : !state.AllowOvertime && started && remainingMilliseconds <= 0 ? TimerStatus.TimeUp
            : state.Running ? TimerStatus.Running
            : started ? TimerStatus.Paused
            : TimerStatus.Ready;

        var tone = overtime ? TimerTone.Overtime
            : started && remainingMilliseconds < LowTimeSeconds * 1000L ? TimerTone.LowTime
            : TimerTone.Normal;

        // Counting down, round up: a timer started at 50:00 shows 50:00 for its first second and
        // only reaches 00:00 at zero. Counting up past zero, round down, like a stopwatch.
        var time = overtime
            ? "+" + TimerFormat.Format((int)(-remainingMilliseconds / 1000))
            : TimerFormat.Format((int)Math.Ceiling(Math.Max(0, remainingMilliseconds) / 1000.0));

        var progress = state.TotalSeconds == 0
            ? 0
            : Math.Min(1, state.ElapsedMilliseconds / (state.TotalSeconds * 1000.0));

        return new TimerReadout(status, tone, time, progress);
    }
}
