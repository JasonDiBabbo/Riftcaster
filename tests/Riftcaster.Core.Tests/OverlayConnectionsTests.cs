using Riftcaster.Core.Overlays;

namespace Riftcaster.Core.Tests;

public class OverlayConnectionsTests
{
    private readonly OverlayConnections _connections = new();

    private int _changedCount;

    public OverlayConnectionsTests()
    {
        _connections.Changed += () => _changedCount++;
    }

    [Fact]
    public void Count_IsZeroInitially()
    {
        Assert.Equal(0, _connections.Count);
    }

    [Fact]
    public void Connect_CountsTheOverlayAndRaisesChanged()
    {
        _connections.Connect();
        _connections.Connect();

        Assert.Equal(2, _connections.Count);
        Assert.Equal(2, _changedCount);
    }

    [Fact]
    public void Dispose_UncountsTheOverlayAndRaisesChanged()
    {
        var first = _connections.Connect();
        _connections.Connect();

        first.Dispose();

        Assert.Equal(1, _connections.Count);
        Assert.Equal(3, _changedCount);
    }

    [Fact]
    public void Dispose_Twice_UncountsOnce()
    {
        var connection = _connections.Connect();
        _connections.Connect();

        connection.Dispose();
        connection.Dispose();

        Assert.Equal(1, _connections.Count);
        Assert.Equal(3, _changedCount);
    }

    [Fact]
    public void ManyOverlaysAtOnce_CountsEveryOne()
    {
        // Overlays connect and disconnect on many threads at once, e.g. when OBS starts.
        var connections = new IDisposable[1000];

        Parallel.For(0, connections.Length, i => connections[i] = _connections.Connect());
        Assert.Equal(connections.Length, _connections.Count);

        Parallel.For(0, connections.Length, i => connections[i].Dispose());
        Assert.Equal(0, _connections.Count);
    }
}
