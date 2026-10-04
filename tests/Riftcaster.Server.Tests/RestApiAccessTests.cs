using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Riftcaster.Core.Network;

namespace Riftcaster.Server.Tests;

// Its own test server (a fixture per class), since these tests set the access code. Each test
// uses its own device address, so one test's wrong codes don't count against another's.
public class RestApiAccessTests(RiftcasterWebApplicationFactory factory) : IClassFixture<RiftcasterWebApplicationFactory>
{
    private const string Code = "K7QM-4XPA";

    private AccessCode AccessCode => factory.Services.GetRequiredService<AccessCode>();

    [Fact]
    public async Task ThisComputer_NeedsNoCode()
    {
        AccessCode.Set(Code);

        var response = await SendAsync("127.0.0.1", HttpMethods.Post, "/api/timer/pause");

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
    }

    [Fact]
    public async Task NoCodeSet_OtherDevices_AreForbidden()
    {
        AccessCode.Remove();

        var response = await SendAsync("192.168.1.70", HttpMethods.Get, "/api/timer", bearer: "ANYTHING");

        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.Equal("No access code is set", Title(response));
    }

    [Fact]
    public async Task CodeSet_OtherDeviceWithoutIt_IsChallenged()
    {
        AccessCode.Set(Code);

        var response = await SendAsync("192.168.1.71", HttpMethods.Get, "/api/timer");

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", response.Headers.WWWAuthenticate);
        Assert.Equal("The access code is needed", Title(response));
    }

    [Fact]
    public async Task RightCode_LetsTheDeviceIn()
    {
        AccessCode.Set(Code);

        var read = await SendAsync("192.168.1.72", HttpMethods.Get, "/api/timer", bearer: Code);
        var change = await SendAsync("192.168.1.72", HttpMethods.Post, "/api/timer/pause", bearer: "k7qm 4xpa"); // As the admin accepts it
        var document = await SendAsync("192.168.1.72", HttpMethods.Get, "/openapi/v1.json", bearer: Code);

        Assert.Equal(StatusCodes.Status200OK, read.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, change.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, document.StatusCode);
    }

    [Fact]
    public async Task WrongCode_IsRefused_AndTooManyLockTheDeviceOut()
    {
        AccessCode.Set(Code);
        const string Device = "192.168.1.73";

        for (var attempt = 0; attempt < OperatorSignIn.AttemptLimit; attempt++)
        {
            var wrong = await SendAsync(Device, HttpMethods.Get, "/api/timer", bearer: "WRONG-CODE");
            Assert.Equal(StatusCodes.Status401Unauthorized, wrong.StatusCode);
            Assert.Equal("That access code isn't right", Title(wrong));
        }

        var locked = await SendAsync(Device, HttpMethods.Get, "/api/timer", bearer: Code);
        var otherDevice = await SendAsync("192.168.1.74", HttpMethods.Get, "/api/timer", bearer: Code);

        Assert.Equal(StatusCodes.Status429TooManyRequests, locked.StatusCode); // Even with the right code
        Assert.Equal(StatusCodes.Status200OK, otherDevice.StatusCode);
    }

    [Fact]
    public async Task ChangedCode_StopsTheOldOne()
    {
        AccessCode.Set(Code);
        AccessCode.Set("NEW-CODE-22");

        var response = await SendAsync("192.168.1.75", HttpMethods.Get, "/api/timer", bearer: Code);

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OverlayStreamPaths_AreOnlyOpenToRead()
    {
        // /api/teams/{team} with the team "events" looks like an overlay's stream (/api/x/events),
        // which other devices may open without the code; a change there still needs it.
        AccessCode.Set(Code);

        var response = await SendAsync("192.168.1.78", HttpMethods.Patch, "/api/teams/events");

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DocsPage_OnOtherDevices_GoesToSignIn()
    {
        AccessCode.Set(Code);

        var response = await SendAsync("192.168.1.76", HttpMethods.Get, "/api/docs");

        Assert.Equal(StatusCodes.Status302Found, response.StatusCode);
        Assert.Equal("/signin", response.Headers.Location);
    }

    [Theory]
    [InlineData("POST", "/api/timer/pause", "http://evil.example", StatusCodes.Status403Forbidden)]
    [InlineData("POST", "/api/timer/pause", "null", StatusCodes.Status403Forbidden)] // A sandboxed page or a file
    [InlineData("POST", "/api/timer/pause", "http://localhost", StatusCodes.Status200OK)] // The server's own pages
    [InlineData("POST", "/api/timer/pause", null, StatusCodes.Status200OK)] // Tools send no Origin
    [InlineData("GET", "/api/timer", "http://evil.example", StatusCodes.Status200OK)] // The browser hides the answer from the other site
    public async Task OtherWebsites_CantMakeChanges(string method, string path, string? origin, int status)
    {
        var response = await SendAsync("127.0.0.1", method, path, origin: origin);

        Assert.Equal(status, response.StatusCode);
    }

    [Fact]
    public async Task OtherWebsites_AreRefusedBeforeTheCodeIsChecked()
    {
        AccessCode.Set(Code);

        var response = await SendAsync("192.168.1.77", HttpMethods.Post, "/api/timer/pause", bearer: Code, origin: "http://evil.example");

        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.Equal("Requests from other websites are refused", Title(response));
    }

    [Theory]
    [InlineData("Bearer K7QM-4XPA", "K7QM-4XPA")]
    [InlineData("bearer  K7QM-4XPA ", "K7QM-4XPA")]
    [InlineData("Basic abc", null)]
    [InlineData("Bearer ", null)]
    [InlineData("", null)]
    public void BearerToken_ReadsTheHeader(string header, string? token)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = header;

        Assert.Equal(token, RemoteAccess.BearerToken(context.Request));
    }

    private static string? Title(Response response) =>
        JsonDocument.Parse(response.Body).RootElement.GetProperty("title").GetString();

    private sealed record Response(int StatusCode, IHeaderDictionary Headers, string Body);

    private async Task<Response> SendAsync(string address, string method, string path, string? bearer = null, string? origin = null)
    {
        var context = await factory.Server.SendAsync(request =>
        {
            request.Request.Method = method;
            request.Request.Path = path;
            request.Connection.RemoteIpAddress = IPAddress.Parse(address);
            if (bearer is not null)
            {
                request.Request.Headers.Authorization = $"Bearer {bearer}";
            }

            if (origin is not null)
            {
                request.Request.Headers.Origin = origin;
            }
        });

        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        return new Response(context.Response.StatusCode, context.Response.Headers, body);
    }
}
