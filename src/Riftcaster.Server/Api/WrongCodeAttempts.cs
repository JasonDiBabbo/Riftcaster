using System.Threading.RateLimiting;

namespace Riftcaster.Server.Api;

/// <summary>
/// Limits wrong access codes sent to the REST API, per device, as the sign-in page limits
/// attempts: <see cref="OperatorSignIn.AttemptLimit"/> per <see cref="OperatorSignIn.AttemptWindow"/>.
/// </summary>
/// <remarks>
/// Only wrong codes count, since a button panel sends the right one on every press. Once a device
/// has used up its attempts, even the right code is refused until the window ends, or guessing
/// would only be slowed down, not stopped.
/// </remarks>
internal sealed class WrongCodeAttempts : IDisposable
{
    private readonly PartitionedRateLimiter<string> _limiter = PartitionedRateLimiter.Create<string, string>(device =>
        RateLimitPartition.GetFixedWindowLimiter(device, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = OperatorSignIn.AttemptLimit,
            Window = OperatorSignIn.AttemptWindow,
            QueueLimit = 0,
        }));

    /// <summary>
    /// Whether the device has used up its attempts for now.
    /// </summary>
    /// <param name="device">The device's address.</param>
    public bool IsLockedOut(string device)
    {
        using var lease = _limiter.AttemptAcquire(device, permitCount: 0); // Checks without using one
        return !lease.IsAcquired;
    }

    /// <summary>
    /// Counts a wrong code from the device.
    /// </summary>
    /// <param name="device">The device's address.</param>
    public void CountWrongCode(string device)
    {
        using var _ = _limiter.AttemptAcquire(device);
    }

    public void Dispose()
    {
        _limiter.Dispose();
    }
}
