using System.Text.Json;

namespace Riftcaster.Server.Tests;

public class RestApiDocsTests(RiftcasterWebApplicationFactory factory) : IClassFixture<RiftcasterWebApplicationFactory>
{
    [Fact]
    public async Task Document_DescribesTheApi()
    {
        var client = factory.CreateClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var root = document.RootElement;
        var paths = root.GetProperty("paths").EnumerateObject().Select(path => path.Name).ToList();

        Assert.Equal("Riftcaster API", root.GetProperty("info").GetProperty("title").GetString());
        Assert.True(root.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("accessCode", out _));
        Assert.Contains("/api/timer/start", paths);
        Assert.Contains("/api/match", paths);
        Assert.Contains("/api/players/{seat}/adjust", paths);
        Assert.Contains("/api/teams/{team}", paths);
        Assert.Contains("/api/featured-card", paths);
        Assert.Contains("/api/cards", paths);
        Assert.Contains("/api/lower-third/messages/{id}/show", paths);

        // Not REST: the overlays' WebSockets and the sign-in pages.
        Assert.DoesNotContain(paths, path => path.EndsWith("/events") || path.StartsWith("/sign"));
    }

    [Fact]
    public async Task DocsPage_IsServed()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/docs");

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }
}
