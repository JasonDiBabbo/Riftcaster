namespace Riftcaster.Contracts;

/// <summary>
/// The match timer, as sent to the overlays: a snapshot taken when it was sent, not one per tick.
/// </summary>
/// <remarks>
/// While the timer runs, each client adds the time since it received this snapshot, using its own
/// clock. Only that difference matters, so it doesn't depend on the client's clock agreeing with
/// the server's.
/// </remarks>
/// <param name="TotalSeconds">The timer's length.</param>
/// <param name="ElapsedMilliseconds">Time run so far, as of when this snapshot was sent.</param>
/// <param name="Running">Whether the timer is counting.</param>
/// <param name="AllowOvertime">
/// Whether the timer keeps counting past zero (shown as <c>+mm:ss</c>). Without overtime, the
/// server stops it at zero.
/// </param>
public record TimerState(int TotalSeconds, long ElapsedMilliseconds, bool Running, bool AllowOvertime);
