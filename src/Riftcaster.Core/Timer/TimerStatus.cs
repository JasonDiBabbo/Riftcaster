namespace Riftcaster.Core.Timer;

/// <summary>
/// The timer's status, as shown beside the Timer panel's title.
/// </summary>
public enum TimerStatus
{
    /// <summary>Not started since the last reset or set.</summary>
    Ready,

    /// <summary>Counting down.</summary>
    Running,

    /// <summary>Paused before reaching zero.</summary>
    Paused,

    /// <summary>Counting up past zero, with overtime allowed.</summary>
    Overtime,

    /// <summary>Paused past zero, with overtime allowed.</summary>
    OvertimePaused,

    /// <summary>Stopped at zero, with overtime off.</summary>
    TimeUp,
}
