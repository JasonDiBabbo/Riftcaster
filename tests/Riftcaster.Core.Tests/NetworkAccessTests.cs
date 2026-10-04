using System.Net;
using Riftcaster.Core.Network;

namespace Riftcaster.Core.Tests;

public class NetworkAccessTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Enabled_StartsAsGiven(bool enabled)
    {
        Assert.Equal(enabled, new NetworkAccess(enabled, [NetworkAccess.DefaultUrl]).Enabled);
    }

    [Fact]
    public void SetEnabled_ChangesItAndRaisesChanged()
    {
        var network = new NetworkAccess(false, [NetworkAccess.DefaultUrl]);
        var changed = 0;
        network.Changed += () => changed++;

        network.SetEnabled(true);

        Assert.True(network.Enabled);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void SetEnabled_SameValue_DoesNothing()
    {
        var network = new NetworkAccess(true, [NetworkAccess.DefaultUrl]);
        var changed = 0;
        network.Changed += () => changed++;

        network.SetEnabled(true);

        Assert.Equal(0, changed);
    }

    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("::ffff:127.0.0.1", true)] // IPv4 loopback, as seen on a dual-mode IPv6 socket
    [InlineData("192.168.1.50", false)]
    [InlineData("::ffff:192.168.1.50", false)]
    [InlineData("fe80::1", false)]
    public void IsLocal(string address, bool expected)
    {
        Assert.Equal(expected, NetworkAccess.IsLocal(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData(null, true, false)] // No option: off
    [InlineData("on", true, true)]
    [InlineData("ON", true, true)]
    [InlineData("off", true, false)]
    [InlineData("yes", false, false)] // Not understood: off, and reported
    [InlineData("", false, false)]
    public void TryParseOption(string? value, bool understood, bool enabled)
    {
        Assert.Equal(understood, NetworkAccess.TryParseOption(value, out var parsed));
        Assert.Equal(enabled, parsed);
    }

    [Theory]
    [InlineData(null, new[] { "http://localhost:5062/" })] // Nothing given: the default
    [InlineData("http://localhost:5063", new[] { "http://localhost:5063/" })]
    [InlineData("https://localhost:7216;http://localhost:5062", new[] { "https://localhost:7216/", "http://localhost:5062/" })]
    [InlineData("http://*:8080", new[] { "http://localhost:8080/" })] // Any address: only the port counts
    [InlineData("not a url", new[] { "http://localhost:5062/" })]
    public void ParseUrls(string? urls, string[] expected)
    {
        Assert.Equal(expected, NetworkAccess.ParseUrls(urls).Select(url => url.ToString()));
    }
}
