using System.Net;
using Microsoft.AspNetCore.Http;
using Riftcaster.Core.Network;

namespace Riftcaster.Server.Tests;

public class NetworkAccessTests(RiftcasterWebApplicationFactory factory) : IClassFixture<RiftcasterWebApplicationFactory>
{
    private static readonly IPAddress OtherDevice = IPAddress.Parse("192.168.1.50");

    // Where the server listens: what Kestrel binds to, and rebinds to when the switch changes.
    [Fact]
    public void Endpoints_Off_ListenOnLocalhostOnly()
    {
        var provider = new NetworkEndpointsProvider(new NetworkAccess(false, NetworkAccess.ParseUrls("https://localhost:7216;http://localhost:5062")));

        provider.Load();

        Assert.True(provider.TryGet("Kestrel:Endpoints:Riftcaster0:Url", out var https));
        Assert.True(provider.TryGet("Kestrel:Endpoints:Riftcaster1:Url", out var http));
        Assert.Equal(("https://localhost:7216", "http://localhost:5062"), (https, http));
    }

    [Fact]
    public void Endpoints_SwitchedOn_ListenEverywhereAndReload()
    {
        var network = new NetworkAccess(false, [NetworkAccess.DefaultUrl]);
        var provider = new NetworkEndpointsProvider(network);
        provider.Load();
        var reloaded = false;
        provider.GetReloadToken().RegisterChangeCallback(_ => reloaded = true, null);

        network.SetEnabled(true);

        Assert.True(reloaded); // What makes Kestrel rebind
        Assert.True(provider.TryGet("Kestrel:Endpoints:Riftcaster0:Url", out var url));
        Assert.Equal("http://*:5062", url);
    }

    // What other devices can reach.
    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("::ffff:127.0.0.1", true)] // IPv4 loopback, as seen on a dual-mode IPv6 socket
    [InlineData("192.168.1.50", false)]
    [InlineData("::ffff:192.168.1.50", false)]
    [InlineData("fe80::1", false)]
    public void IsLocal(string address, bool expected)
    {
        Assert.Equal(expected, RemoteAccess.IsLocal(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("/overlays/timer/timer.html", true)]
    [InlineData("/overlays/fonts/cinzel.woff2", true)]
    [InlineData("/api/info", true)]
    [InlineData("/api/timer/events", true)]
    [InlineData("/api/featured-card/events", true)]
    [InlineData("/", false)] // The admin
    [InlineData("/_blazor", false)] // The admin's live connection
    [InlineData("/_framework/blazor.web.js", false)]
    [InlineData("/api/timer", false)] // Not a stream
    [InlineData("/api/timer/events/extra", false)]
    [InlineData("/overlaysx/timer.html", false)]
    public void IsOpenToTheNetwork(string path, bool expected)
    {
        Assert.Equal(expected, RemoteAccess.IsOpenToTheNetwork(path));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/_blazor/negotiate")]
    public async Task OtherDevice_Admin_IsForbidden(string path)
    {
        var response = await SendAsync(path, OtherDevice);

        Assert.Equal(StatusCodes.Status403Forbidden, response.Response.StatusCode);
    }

    [Fact]
    public async Task OtherDevice_ApiInfo_IsAllowed()
    {
        var response = await SendAsync("/api/info", OtherDevice);

        Assert.Equal(StatusCodes.Status200OK, response.Response.StatusCode);
    }

    [Fact]
    public async Task ThisComputer_Admin_IsAllowed()
    {
        var response = await SendAsync("/", IPAddress.Loopback);

        Assert.Equal(StatusCodes.Status200OK, response.Response.StatusCode);
    }

    private Task<HttpContext> SendAsync(string path, IPAddress from) =>
        factory.Server.SendAsync(context =>
        {
            context.Request.Method = HttpMethods.Get;
            context.Request.Path = path;
            context.Connection.RemoteIpAddress = from;
        });
}
