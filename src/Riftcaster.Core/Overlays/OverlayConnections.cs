namespace Riftcaster.Core.Overlays;

/// <summary>
/// How many overlays are connected to the server right now, for the admin header.
/// </summary>
/// <remarks>
/// Counts open state streams. Each overlay page opens one, so this is the number of overlay pages
/// (OBS sources, or browser tabs) currently showing live data.
/// </remarks>
public sealed class OverlayConnections
{
    private int _count;

    /// <summary>
    /// The number of overlays connected now.
    /// </summary>
    public int Count => Volatile.Read(ref _count);

    /// <summary>
    /// Raised after <see cref="Count"/> changes. Handlers read <see cref="Count"/> for the new value.
    /// </summary>
    /// <remarks>
    /// May be raised on any thread.
    /// </remarks>
    public event Action? Changed;

    /// <summary>
    /// Counts an overlay as connected until the returned handle is disposed.
    /// </summary>
    /// <returns>A handle to dispose when the overlay disconnects. Disposing it more than once is harmless.</returns>
    public IDisposable Connect()
    {
        Interlocked.Increment(ref _count);
        Changed?.Invoke();
        return new Connection(this);
    }

    private void Disconnect()
    {
        Interlocked.Decrement(ref _count);
        Changed?.Invoke();
    }

    /// <summary>
    /// One connected overlay. Uncounts it on the first dispose only.
    /// </summary>
    private sealed class Connection(OverlayConnections connections) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                connections.Disconnect();
            }
        }
    }
}
