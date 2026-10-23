using System.Net;
using System.Net.Sockets;
using Riftcaster.Core.Network;

namespace Riftcaster.Core.Tests;

public class PortProbeTests
{
    // A port nothing has: asked for, then let go of.
    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    public void IsFree_ForAPortNothingHas()
    {
        Assert.True(PortProbe.IsFree(FreePort(), allInterfaces: false));
    }

    [Fact]
    public void IsFree_AfterTheProbeItself()
    {
        var port = FreePort();

        Assert.True(PortProbe.IsFree(port, allInterfaces: false));
        Assert.True(PortProbe.IsFree(port, allInterfaces: false)); // It lets go of the port
    }

    [Fact]
    public void IsFree_IsFalseForAPortAnotherProgramHolds()
    {
        using var holder = new TcpListener(IPAddress.Loopback, 0);
        holder.Start();
        var port = ((IPEndPoint)holder.LocalEndpoint).Port;

        Assert.False(PortProbe.IsFree(port, allInterfaces: false));
    }

    // Windows lets the server bind localhost on a port another program holds on every interface, and then
    // shares it unpredictably, so binding alone says it's free.
    [Fact]
    public void IsFree_IsFalseForAPortAnotherProgramHoldsOnEveryInterface()
    {
        using var holder = new TcpListener(IPAddress.Any, 0);
        holder.Start();
        var port = ((IPEndPoint)holder.LocalEndpoint).Port;

        Assert.False(PortProbe.IsFree(port, allInterfaces: false));
    }

    [Fact]
    public void IsFree_IsTrueAgainOnceItIsReleased()
    {
        var holder = new TcpListener(IPAddress.Loopback, 0);
        holder.Start();
        var port = ((IPEndPoint)holder.LocalEndpoint).Port;
        Assert.False(PortProbe.IsFree(port, allInterfaces: false));

        holder.Stop();

        Assert.True(PortProbe.IsFree(port, allInterfaces: false));
    }

    // With network access on, the server listens on every interface, so one held on any is a conflict.
    [Fact]
    public void IsFree_OnEveryInterface_IsFalseForAPortHeldOnAny()
    {
        using var holder = new TcpListener(IPAddress.Any, 0);
        holder.Start();
        var port = ((IPEndPoint)holder.LocalEndpoint).Port;

        Assert.False(PortProbe.IsFree(port, allInterfaces: true));
    }

    [Fact]
    public void IsFree_OnEveryInterface_IsTrueForAPortNothingHas()
    {
        Assert.True(PortProbe.IsFree(FreePort(), allInterfaces: true));
    }
}
