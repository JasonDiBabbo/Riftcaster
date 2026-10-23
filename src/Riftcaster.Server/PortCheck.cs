using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting.Server;
using Riftcaster.Core.Network;

namespace Riftcaster.Server;

/// <summary>
/// Stops the server from starting on a port another program already has, before it binds it.
/// </summary>
/// <remarks>
/// Binding doesn't always fail for that: Windows lets the server bind localhost on a port another program
/// holds on every interface (a Python or Node server, say), and then the two share it, so connections
/// go to either. This asks who is listening (<see cref="PortProbe"/>), and fails the way Kestrel does
/// for a port that's taken, so the launchers report it in one way (see <see cref="PortConflict"/>).
/// Runs ahead of the web server's own start, which is registered after every other hosted service.
/// </remarks>
internal sealed class PortCheck : IHostedService
{
    private readonly NetworkAccess _network;

    private readonly bool _listens;

    // Only Kestrel listens on a real port: the server tests start doesn't, and has none to check. (Its type is
    // an internal one of Kestrel's, so it's known by where it lives.)
    public PortCheck(NetworkAccess network, IServer server)
        : this(network, server.GetType().Namespace?.StartsWith("Microsoft.AspNetCore.Server.Kestrel", StringComparison.Ordinal) == true)
    {
    }

    internal PortCheck(NetworkAccess network, bool listens)
    {
        _network = network;
        _listens = listens;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_listens
            && ListenPort.HttpPort(_network.ListenUrls) is { } port
            && !PortProbe.IsFree(port, _network.Enabled))
        {
            throw new AddressInUseException($"Port {port} is in use by another program.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
