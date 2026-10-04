namespace Riftcaster.Core.Timer;

/// <summary>
/// Why typed text isn't a time (see <see cref="TimerFormat.Check"/>), so the admin can say what to fix.
/// </summary>
public enum TimeEntryError
{
    /// <summary>
    /// It's a time.
    /// </summary>
    None,

    /// <summary>
    /// Not <c>mm:ss</c> or whole minutes: empty, letters, seconds of 60 or more, and so on.
    /// </summary>
    Format,

    /// <summary>
    /// A time with a minus sign.
    /// </summary>
    Negative,

    /// <summary>
    /// A time, but longer than <see cref="TimerFormat.MaxMinutes"/>:59.
    /// </summary>
    TooLong,
}
