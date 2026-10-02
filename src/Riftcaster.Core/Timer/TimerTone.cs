namespace Riftcaster.Core.Timer;

/// <summary>
/// How urgently the time is shown: its colour in the panel.
/// </summary>
public enum TimerTone
{
    /// <summary>More than <see cref="TimerReadout.LowTimeSeconds"/> left, or not started.</summary>
    Normal,

    /// <summary>Started, with less than <see cref="TimerReadout.LowTimeSeconds"/> left.</summary>
    LowTime,

    /// <summary>Past zero.</summary>
    Overtime,
}
