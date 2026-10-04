using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Riftcaster.Core.Overlays;

namespace Riftcaster.Server.Tests;

// The tests run as Development, so these are the repository's built overlays (src/overlays/dist).
// tools/publish/check-publish.mjs checks a published build's own copy, in CI.
public class OverlayHostingTests(RiftcasterWebApplicationFactory factory) : IClassFixture<RiftcasterWebApplicationFactory>
{
    [Fact]
    public void OverlayFiles_AreFound()
    {
        var files = factory.Services.GetRequiredService<OverlayFiles>();

        Assert.True(files.Found, $"No overlays at {files.Folder}");
    }

    [Fact]
    public async Task OverlayPage_IsServed()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/overlays/timer/timer.html");

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    // A wrong or missing Overlays:Path doesn't stop the server, but the admin header says so.
    [Theory]
    [InlineData("no-such-folder")]
    [InlineData("")]
    public async Task NoOverlayFiles_AdminSaysSo(string configuredPath)
    {
        using var server = factory.WithWebHostBuilder(builder => builder.UseSetting("Overlays:Path", configuredPath));
        var client = server.CreateClient();

        Assert.False(server.Services.GetRequiredService<OverlayFiles>().Found);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/overlays/timer/timer.html")).StatusCode);
        Assert.Contains("Overlay files missing", await client.GetStringAsync("/"));
    }
}
