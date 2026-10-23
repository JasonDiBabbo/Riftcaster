using Riftcaster.Core.Network;

namespace Riftcaster.Server.Tests;

// The addresses Kestrel is told to listen on, from NetworkAccess: it reloads them, and rebinds, when they change.
public class NetworkEndpointsProviderTests
{
    private const string Key = "Kestrel:Endpoints:Riftcaster0:Url";

    private static string? Url(NetworkEndpointsProvider provider) => provider.TryGet(Key, out var value) ? value : null;

    private static NetworkEndpointsProvider Loaded(NetworkAccess network)
    {
        var provider = new NetworkEndpointsProvider(network);
        provider.Load();
        return provider;
    }

    [Fact]
    public void Load_LocalhostWhileNetworkAccessIsOff()
    {
        Assert.Equal("http://localhost:5062", Url(Loaded(new NetworkAccess(false, [NetworkAccess.DefaultUrl]))));
    }

    [Fact]
    public void Load_EveryInterfaceWhileItIsOn()
    {
        Assert.Equal("http://*:5062", Url(Loaded(new NetworkAccess(true, [NetworkAccess.DefaultUrl]))));
    }

    [Fact]
    public void ChangingThePort_ReloadsTheEndpointsAtTheNewPort()
    {
        var network = new NetworkAccess(false, [NetworkAccess.DefaultUrl]);
        var provider = Loaded(network);
        var token = provider.GetReloadToken();

        network.SetPort(5070);

        Assert.True(token.HasChanged); // Kestrel watches this, and rebinds
        Assert.Equal("http://localhost:5070", Url(provider));
    }

    [Fact]
    public void ChangingNetworkAccess_StillReloadsThem()
    {
        var network = new NetworkAccess(false, [NetworkAccess.DefaultUrl]);
        var provider = Loaded(network);
        var token = provider.GetReloadToken();

        network.SetEnabled(true);

        Assert.True(token.HasChanged);
        Assert.Equal("http://*:5062", Url(provider));
    }

    [Fact]
    public void ChangingBoth_KeepsBothChanges()
    {
        var network = new NetworkAccess(false, [NetworkAccess.DefaultUrl]);
        var provider = Loaded(network);

        network.SetEnabled(true);
        network.SetPort(5071);

        Assert.Equal("http://*:5071", Url(provider));
    }

    [Fact]
    public void ChangingThePortBackAndForth_FollowsEachTime()
    {
        var network = new NetworkAccess(false, [NetworkAccess.DefaultUrl]);
        var provider = Loaded(network);

        network.SetPort(5070);
        network.SetPort(NetworkAccess.DefaultUrl.Port);

        Assert.Equal("http://localhost:5062", Url(provider));
    }

    [Fact]
    public void EveryAddressMoves()
    {
        var network = new NetworkAccess(false, [new Uri("http://localhost:5062"), new Uri("https://localhost:7216")]);
        var provider = Loaded(network);

        network.SetPort(5070);

        Assert.Equal("http://localhost:5070", Url(provider));
        Assert.True(provider.TryGet("Kestrel:Endpoints:Riftcaster1:Url", out var second));
        Assert.Equal("https://localhost:5070", second);
    }
}
