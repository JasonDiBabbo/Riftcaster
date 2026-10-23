using Microsoft.Extensions.Logging;
using Riftcaster.Contracts;
using Riftcaster.Core.Settings;

namespace Riftcaster.Core.Network;

/// <summary>
/// How a switch to another port ended.
/// </summary>
/// <param name="Switched">Whether the server is now on the port.</param>
/// <param name="Problem">What went wrong, in words for the operator, or null.</param>
public sealed record PortSwitchResult(bool Switched, string? Problem)
{
    /// <summary>It worked.</summary>
    public static PortSwitchResult Done { get; } = new(true, null);

    /// <summary>It didn't, for a reason the operator can read.</summary>
    /// <param name="problem">The reason.</param>
    public static PortSwitchResult Failed(string problem) => new(false, problem);
}

/// <summary>
/// Moves the running server to another port, without restarting it, and makes sure it worked.
/// </summary>
/// <remarks>
/// <para>
/// Telling the server to listen somewhere else (<see cref="NetworkAccess.SetPort"/>) closes the old port and
/// binds the new, and if that can't be done, as when another program holds the new port, the server is left
/// running and listening on nothing. So it's checked first (<see cref="PortProbe"/>), and afterwards this
/// asks the new port who answers: if it isn't this server within the time allowed, it goes back to the
/// old port, so there is always a port that works.
/// </para>
/// <para>
/// The web interface's pages are told before the old port closes (<see cref="Moving"/>), so each can go to the
/// new address by itself. Overlays in streaming software can't: they need their new address typed in.
/// </para>
/// </remarks>
public sealed class PortSwitcher
{
    /// <summary>
    /// How long the pages have to be told of a move before the old port closes.
    /// </summary>
    public static readonly TimeSpan DefaultGrace = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How long to wait for the server to answer on a port after moving to it. Moving takes a few seconds
    /// while anything is connected: the old port is closed gracefully first.
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// How often to ask while waiting.
    /// </summary>
    public static readonly TimeSpan DefaultPoll = TimeSpan.FromMilliseconds(250);

    private readonly NetworkAccess _network;

    private readonly AppSettingsService _settings;

    private readonly Func<int, CancellationToken, Task<bool>> _isServing;

    private readonly ILogger<PortSwitcher> _logger;

    private readonly TimeProvider _time;

    private readonly TimeSpan _grace;

    private readonly TimeSpan _timeout;

    private readonly TimeSpan _poll;

    private int _busy;

    private volatile int _switchingTo;

    /// <summary>
    /// Creates the switcher.
    /// </summary>
    /// <param name="network">Where the server listens, and moves.</param>
    /// <param name="settings">Where the chosen port is kept, which a switch leaves matching the one in use.</param>
    /// <param name="isServing">Whether this server answers on a port. Given the port, it says yes or no.</param>
    /// <param name="time">The clock, which tests make quick.</param>
    /// <param name="logger">Logs each switch, and what went wrong.</param>
    /// <param name="grace">How long the pages have to be told, or <see cref="DefaultGrace"/>.</param>
    /// <param name="timeout">How long to wait for the new port, or <see cref="DefaultTimeout"/>.</param>
    /// <param name="poll">How often to ask meanwhile, or <see cref="DefaultPoll"/>.</param>
    public PortSwitcher(
        NetworkAccess network,
        AppSettingsService settings,
        Func<int, CancellationToken, Task<bool>> isServing,
        TimeProvider time,
        ILogger<PortSwitcher> logger,
        TimeSpan? grace = null,
        TimeSpan? timeout = null,
        TimeSpan? poll = null)
    {
        _network = network;
        _settings = settings;
        _isServing = isServing;
        _time = time;
        _logger = logger;
        _grace = grace ?? DefaultGrace;
        _timeout = timeout ?? DefaultTimeout;
        _poll = poll ?? DefaultPoll;
    }

    /// <summary>
    /// The port a switch is moving to, or null if none is.
    /// </summary>
    public int? SwitchingTo => _switchingTo == 0 ? null : _switchingTo;

    /// <summary>
    /// The HTTP port the server is listening on, or null if it has only https addresses.
    /// </summary>
    public int? RunningPort => ListenPort.HttpPort(_network.ListenUrls);

    /// <summary>
    /// Raised when a switch starts and when it ends, however it ends. Handlers read <see cref="SwitchingTo"/>.
    /// </summary>
    /// <remarks>
    /// May be raised on any thread.
    /// </remarks>
    public event Action? Changed;

    /// <summary>
    /// Raised when a switch is about to close the old port, with the port it's moving to. Every open page of
    /// the web interface, on this computer or another, follows to it.
    /// </summary>
    /// <remarks>
    /// May be raised on any thread.
    /// </remarks>
    public event Action<int>? Moving;

    /// <summary>
    /// Moves the server to a port and waits until it answers there. If it can't, puts it back where it was.
    /// </summary>
    /// <remarks>
    /// Takes a few seconds, and the connection of whoever asked is closed meanwhile (the old port closing),
    /// so it mustn't depend on that connection: its work carries on without it.
    /// </remarks>
    /// <param name="port">The port to move to.</param>
    /// <param name="cancellationToken">Stops waiting, such as when the server is stopping.</param>
    /// <returns>Whether it moved, and if not, why.</returns>
    public async Task<PortSwitchResult> SwitchAsync(int port, CancellationToken cancellationToken = default)
    {
        if (!ListenPort.IsValid(port))
        {
            return PortSwitchResult.Failed($"Use a port from {ListenPort.Min} to {ListenPort.Max}.");
        }

        if (RunningPort is not { } old)
        {
            return PortSwitchResult.Failed("This run has no HTTP port to move.");
        }

        if (port == old)
        {
            return PortSwitchResult.Done;
        }

        // One at a time: a second switch while one is closing the port would race it.
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
        {
            return PortSwitchResult.Failed("Riftcaster is already switching ports.");
        }

        try
        {
            if (!PortProbe.IsFree(port, _network.Enabled))
            {
                return PortSwitchResult.Failed($"Port {port} is in use by another program.");
            }

            _switchingTo = port;
            Changed?.Invoke();
            Moving?.Invoke(port);

            // Time for the pages to hear it: each then waits for the new port, and goes there.
            await Task.Delay(_grace, _time, cancellationToken);

            _logger.LogInformation("Switching from port {Old} to port {New}.", old, port);
            _network.SetPort(port);

            if (await WaitUntilServingAsync(port, cancellationToken))
            {
                ChoosePort(port);
                _logger.LogInformation("Now listening on port {Port}.", port);
                return PortSwitchResult.Done;
            }

            _logger.LogError("This server isn't answering on port {New} (is something else using it?). Going back to port {Old}.", port, old);
            _network.SetPort(old);
            var back = await WaitUntilServingAsync(old, cancellationToken);
            ChoosePort(old);

            if (back)
            {
                _logger.LogWarning("Back on port {Old}.", old);
                return PortSwitchResult.Failed($"Couldn't start on port {port}, so Riftcaster is back on port {old}.");
            }

            _logger.LogCritical("Not answering on port {Old} either: Riftcaster has to be restarted.", old);
            return PortSwitchResult.Failed($"Couldn't start on port {port}, and couldn't go back to port {old}. Restart Riftcaster.");
        }
        finally
        {
            _switchingTo = 0;
            Interlocked.Exchange(ref _busy, 0);
            Changed?.Invoke();
        }
    }

    // The chosen port follows the one in use, so a restart comes back to it.
    private void ChoosePort(int port) => _settings.ChoosePort(port);

    private async Task<bool> WaitUntilServingAsync(int port, CancellationToken cancellationToken)
    {
        var deadline = _time.GetUtcNow() + _timeout;
        while (true)
        {
            try
            {
                if (await _isServing(port, cancellationToken))
                {
                    return true;
                }
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Not up yet, or not this server, or no answer in time (a timeout is also a cancellation): the
                // same as no. Only being told to stop is not.
            }

            if (_time.GetUtcNow() >= deadline)
            {
                return false;
            }

            await Task.Delay(_poll, _time, cancellationToken);
        }
    }
}
