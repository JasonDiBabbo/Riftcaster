using System.Net;
using System.Net.Http.Json;
using Riftcaster.Contracts;

namespace Riftcaster.Server.Tests;

// /api/info says who the server is, to a page of the web interface that is moving to another port with it: the page
// has to read the answer from the new port, which a browser lets it do only if the server says so. It says so to
// a page from the same host, and to no one else.
public class SameHostCorsTests(RiftcasterWebApplicationFactory factory) : IClassFixture<RiftcasterWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<HttpResponseMessage> InfoAsync(string? origin, string host = "localhost:5062")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/info");
        request.Headers.Host = host;
        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }

        return await _client.SendAsync(request);
    }

    private static string? AllowedOrigin(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values) ? values.Single() : null;

    [Theory]
    [InlineData("http://localhost:5071", "localhost:5062")] // The page that's moving, on the old port
    [InlineData("http://localhost:5062", "localhost:5071")] // And back
    [InlineData("http://LOCALHOST:5071", "localhost:5062")] // Whatever the case
    [InlineData("http://192.168.1.20:5071", "192.168.1.20:5062")] // An operator's phone, on the server's network address
    [InlineData("https://localhost:7216", "localhost:5062")] // Whatever the scheme
    public async Task ThePageOfTheSameHost_MayReadIt(string origin, string host)
    {
        var response = await InfoAsync(origin, host);

        Assert.Equal(origin, AllowedOrigin(response));
    }

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("http://example.com:5062")] // Another site on the same port
    [InlineData("http://localhost.evil.test:5062")]
    [InlineData("http://192.168.1.99:5062")] // Another computer
    [InlineData("null")]
    [InlineData("not a url")]
    [InlineData("")]
    public async Task ThePageOfAnyOtherHost_IsToldNothing(string origin)
    {
        var response = await InfoAsync(origin);

        Assert.Null(AllowedOrigin(response));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // The answer itself is as it was
    }

    [Fact]
    public async Task WithNoOrigin_ThereIsNoHeader()
    {
        var response = await InfoAsync(origin: null);

        Assert.Null(AllowedOrigin(response));
    }

    // A cache must not hand one page's answer, with its allowed origin, to another's.
    [Theory]
    [InlineData("http://localhost:5071")]
    [InlineData("http://example.com")]
    [InlineData(null)]
    public async Task TheAnswerDependsOnTheOrigin(string? origin)
    {
        var response = await InfoAsync(origin);

        Assert.Contains("Origin", response.Headers.Vary);
    }

    [Fact]
    public async Task TheAnswerIsStillWhoTheServerIs()
    {
        var response = await InfoAsync("http://localhost:5071");

        var identity = await response.Content.ReadFromJsonAsync<ServerIdentity>();

        Assert.Equal("Riftcaster Server", identity!.Name);
        Assert.NotEqual(default, identity.StartTime);
    }
}
