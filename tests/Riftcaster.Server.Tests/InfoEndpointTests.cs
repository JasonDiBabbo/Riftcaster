using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Riftcaster.Contracts;

namespace Riftcaster.Server.Tests;

public class InfoEndpointTests(RiftcasterWebApplicationFactory factory) : IClassFixture<RiftcasterWebApplicationFactory>
{
    [Fact]
    public async Task GetInfo_ReturnsServerIdentity()
    {
        var client = factory.CreateClient();
        var identity = await client.GetFromJsonAsync<ServerIdentity>("/api/info");

        Assert.NotNull(identity);
        Assert.Equal("Riftcaster Server", identity.Name);
    }

    [Fact]
    public async Task BlazorScript_IsServed()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/_framework/blazor.web.js");
        response.EnsureSuccessStatusCode();
    }
}
