using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Connections;
using Riftcaster.Core.Network;

namespace Riftcaster.Server.Tests;

// Refusing to start on a port another program has, including one it holds on every interface, which
// binding the server's own address doesn't notice.
public class PortCheckTests
{
    private static NetworkAccess On(int port, bool network = false) => new(network, [new Uri($"http://localhost:{port}")]);

    private static TcpListener Hold(IPAddress address)
    {
        var listener = new TcpListener(address, 0);
        listener.Start();
        return listener;
    }

    private static int PortOf(TcpListener listener) => ((IPEndPoint)listener.LocalEndpoint).Port;

    [Fact]
    public async Task AFreePort_StartsNormally()
    {
        using var holder = Hold(IPAddress.Loopback);
        var port = PortOf(holder);
        holder.Stop();

        await new PortCheck(On(port), listens: true).StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task APortAnotherProgramHolds_FailsAsAnAddressInUse()
    {
        using var holder = Hold(IPAddress.Loopback);

        var exception = await Assert.ThrowsAsync<AddressInUseException>(() =>
            new PortCheck(On(PortOf(holder)), listens: true).StartAsync(CancellationToken.None));

        Assert.True(PortConflict.IsPortInUse(exception));
    }

    // The case binding misses: held on every interface, while the server would bind localhost.
    [Fact]
    public async Task APortHeldOnEveryInterface_FailsToo()
    {
        using var holder = Hold(IPAddress.Any);

        await Assert.ThrowsAsync<AddressInUseException>(() =>
            new PortCheck(On(PortOf(holder)), listens: true).StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ServerThatDoesNotListen_IsNotChecked()
    {
        using var holder = Hold(IPAddress.Loopback);

        await new PortCheck(On(PortOf(holder)), listens: false).StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WithOnlyHttpsAddresses_HasNoPortToCheck()
    {
        var network = new NetworkAccess(false, [new Uri("https://localhost:5099")]);

        await new PortCheck(network, listens: true).StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Stopping_DoesNothing()
    {
        await new PortCheck(On(5062), listens: true).StopAsync(CancellationToken.None);
    }
}
