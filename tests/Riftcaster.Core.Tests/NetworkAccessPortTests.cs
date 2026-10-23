using Riftcaster.Core.Network;

namespace Riftcaster.Core.Tests;

// Moving the port while the server runs: NetworkAccess says where it's to listen, and the server follows.
public class NetworkAccessPortTests
{
    private static Uri[] Urls(params string[] urls) => [.. urls.Select(url => new Uri(url))];

    [Fact]
    public void SetPort_ChangesTheListenAddressAndRaisesPortChanged()
    {
        var network = new NetworkAccess(false, [NetworkAccess.DefaultUrl]);
        var raised = 0;
        network.PortChanged += () => raised++;

        network.SetPort(5070);

        Assert.Equal(5070, Assert.Single(network.ListenUrls).Port);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void SetPort_KeepsTheSchemeAndHost()
    {
        var network = new NetworkAccess(false, Urls("https://localhost:7216", "http://localhost:5062"));

        network.SetPort(5070);

        Assert.Equal(["https://localhost:5070/", "http://localhost:5070/"], network.ListenUrls.Select(url => url.ToString()));
    }

    [Fact]
    public void SetPort_ToTheSamePort_DoesNothing()
    {
        var network = new NetworkAccess(false, [NetworkAccess.DefaultUrl]);
        var raised = 0;
        network.PortChanged += () => raised++;

        network.SetPort(NetworkAccess.DefaultUrl.Port);

        Assert.Equal(0, raised);
    }

    // The network switch has its own event: the server logs "network access is on" on that, not for a port.
    [Fact]
    public void SetPort_DoesNotRaiseChanged()
    {
        var network = new NetworkAccess(false, [NetworkAccess.DefaultUrl]);
        var changed = 0;
        network.Changed += () => changed++;

        network.SetPort(5070);

        Assert.Equal(0, changed);
    }

    [Fact]
    public void SetEnabled_DoesNotRaisePortChanged()
    {
        var network = new NetworkAccess(false, [NetworkAccess.DefaultUrl]);
        var raised = 0;
        network.PortChanged += () => raised++;

        network.SetEnabled(true);

        Assert.Equal(0, raised);
    }

    [Fact]
    public void NetworkUrls_FollowTheNewPort()
    {
        var network = new NetworkAccess(false, [NetworkAccess.DefaultUrl]);

        network.SetPort(5070);

        Assert.All(network.NetworkUrls(), url => Assert.EndsWith(":5070", url));
    }
}
