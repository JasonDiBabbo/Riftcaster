using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Riftcaster.Contracts;

namespace Riftcaster.Server.Tests;

public class InfoEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
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