using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Riftcaster.Server.Tests;

public class HostCheckTests(RiftcasterWebApplicationFactory factory) : IClassFixture<RiftcasterWebApplicationFactory>
{
    private static readonly IReadOnlySet<string> Names = HostCheck.AllowedNames("riftcaster.lan; obs.example.com.");

    [Theory]
    [InlineData("localhost")]
    [InlineData("LOCALHOST:5062")]
    [InlineData("localhost.")] // The same name, written in full
    [InlineData("obs.localhost")] // Browsers keep *.localhost on this computer
    [InlineData("127.0.0.1:5062")]
    [InlineData("192.168.1.20")]
    [InlineData("[::1]:5062")]
    [InlineData("[fe80::1]")]
    [InlineData("riftcaster.lan")] // From the setting, ignoring case and spaces
    [InlineData("OBS.example.com:5062")]
    [InlineData("obs.example.com.")]
    public void Answered(string host)
    {
        Assert.True(HostCheck.IsAllowed(new HostString(host), Names));
    }

    [Theory]
    [InlineData("evil.example")] // A rebinding attacker's domain
    [InlineData("evil.example:5062")]
    [InlineData("localhost.evil.example")]
    [InlineData("127.0.0.1.evil.example")]
    [InlineData("riftcaster.lan.evil.example")]
    [InlineData("")]
    public void Refused(string host)
    {
        Assert.False(HostCheck.IsAllowed(new HostString(host), Names));
    }

    [Fact]
    public void ThisComputersOwnNames_AreAnswered()
    {
        var names = HostCheck.AllowedNames(null);

        Assert.True(HostCheck.IsAllowed(new HostString(Dns.GetHostName()), names));
        Assert.True(HostCheck.IsAllowed(new HostString(Environment.MachineName.ToLowerInvariant()), names));
        Assert.True(HostCheck.IsAllowed(new HostString($"{Dns.GetHostName()}.local:5062"), names));
    }

    [Theory]
    [InlineData("/")] // The admin
    [InlineData("/api/timer")] // The REST API
    [InlineData("/overlays/timer/timer.html")] // The overlays
    [InlineData("/api/timer/events")] // Their streams
    public async Task RebindingStyleHost_GetsNothing(string path)
    {
        var response = await SendAsync(factory, path, "evil.example:5062");

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.StartsWith("Riftcaster only answers requests addressed to this computer", response.Body);
    }

    [Theory]
    [InlineData("localhost:5062")]
    [InlineData("127.0.0.1:5062")]
    public async Task ThisComputer_IsAnswered(string host)
    {
        var response = await SendAsync(factory, "/api/timer", host);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
    }

    [Fact]
    public async Task ExtraNameFromTheSetting_IsAnswered()
    {
        using var server = factory.WithWebHostBuilder(builder => builder.UseSetting("HostNames", "riftcaster.lan"));

        var allowed = await SendAsync(server, "/api/timer", "riftcaster.lan:5062");
        var other = await SendAsync(server, "/api/timer", "other.lan:5062");

        Assert.Equal(StatusCodes.Status200OK, allowed.StatusCode);
        Assert.Equal(StatusCodes.Status400BadRequest, other.StatusCode);
    }

    private static async Task<(int StatusCode, string Body)> SendAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> server, string path, string host)
    {
        var context = await server.Server.SendAsync(request =>
        {
            request.Request.Method = HttpMethods.Get;
            request.Request.Path = path;
            request.Request.Host = new HostString(host);
        });

        return (context.Response.StatusCode, await new StreamReader(context.Response.Body).ReadToEndAsync());
    }
}
