using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Riftcaster.Core.Network;

namespace Riftcaster.Server.Tests;

// Its own test server (a fixture per class), since these tests set the access code. Each test
// uses its own device address, so one test's sign-in attempts don't count against another's.
public partial class OperatorSignInTests(RiftcasterWebApplicationFactory factory) : IClassFixture<RiftcasterWebApplicationFactory>
{
    private const string Code = "K7QM-4XPA";

    private AccessCode AccessCode => factory.Services.GetRequiredService<AccessCode>();

    [Fact]
    public async Task NoCode_RemoteAdmin_IsForbidden()
    {
        AccessCode.Remove();
        var device = new RemoteDevice(factory, "192.168.1.60");

        var response = await device.GetAsync("/");

        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CodeSet_RemoteAdmin_RedirectsToSignIn()
    {
        AccessCode.Set(Code);
        var device = new RemoteDevice(factory, "192.168.1.61");

        var response = await device.GetAsync("/");

        Assert.Equal(StatusCodes.Status302Found, response.StatusCode);
        Assert.Equal("/signin", response.Headers.Location);
    }

    [Fact]
    public async Task CodeSet_RemoteLiveConnection_IsUnauthorized()
    {
        AccessCode.Set(Code);
        var device = new RemoteDevice(factory, "192.168.1.62");

        var response = await device.GetAsync("/_blazor/negotiate");

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SignIn_RightCode_LetsTheDeviceIn()
    {
        AccessCode.Set(Code);
        var device = new RemoteDevice(factory, "192.168.1.63");

        var signIn = await device.SignInAsync("k7qm 4xpa"); // As typed on a phone
        var admin = await device.GetAsync("/");

        Assert.Equal(StatusCodes.Status302Found, signIn.StatusCode);
        Assert.Equal("/", signIn.Headers.Location);
        Assert.Equal(StatusCodes.Status200OK, admin.StatusCode);
    }

    [Fact]
    public async Task SignIn_WrongCode_ShowsTheFormAgain()
    {
        AccessCode.Set(Code);
        var device = new RemoteDevice(factory, "192.168.1.64");

        var signIn = await device.SignInAsync("WRONG-CODE");
        var admin = await device.GetAsync("/");

        Assert.Equal(StatusCodes.Status401Unauthorized, signIn.StatusCode);
        Assert.Contains("isn't right", WebUtility.HtmlDecode(signIn.Body));
        Assert.Equal(StatusCodes.Status302Found, admin.StatusCode); // Still not signed in
    }

    [Fact]
    public async Task SignIn_WithoutTheFormsToken_IsRejected()
    {
        AccessCode.Set(Code);
        var device = new RemoteDevice(factory, "192.168.1.65");

        // A post from another site's page, which can't read the sign-in form's token.
        var response = await device.PostFormAsync("/signin", new() { ["code"] = Code });

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangingTheCode_SignsTheDeviceOut()
    {
        AccessCode.Set(Code);
        var device = new RemoteDevice(factory, "192.168.1.66");
        await device.SignInAsync(Code);

        AccessCode.Set("NEWC-ODE7");
        var admin = await device.GetAsync("/");

        Assert.Equal(StatusCodes.Status302Found, admin.StatusCode);
        Assert.Equal("/signin", admin.Headers.Location);
    }

    [Fact]
    public async Task RemovingTheCode_SignsTheDeviceOut()
    {
        AccessCode.Set(Code);
        var device = new RemoteDevice(factory, "192.168.1.67");
        await device.SignInAsync(Code);

        AccessCode.Remove();
        var admin = await device.GetAsync("/");

        Assert.Equal(StatusCodes.Status403Forbidden, admin.StatusCode);
    }

    [Fact]
    public async Task SignOut_SignsTheDeviceOut()
    {
        AccessCode.Set(Code);
        var device = new RemoteDevice(factory, "192.168.1.68");
        await device.SignInAsync(Code);

        await device.PostFormAsync("/signout", []);
        var admin = await device.GetAsync("/");

        Assert.Equal(StatusCodes.Status302Found, admin.StatusCode);
    }

    [Fact]
    public async Task TooManyAttempts_AreRefused()
    {
        AccessCode.Set(Code);
        var device = new RemoteDevice(factory, "192.168.1.69");

        for (var i = 0; i < OperatorSignIn.AttemptLimit; i++)
        {
            await device.SignInAsync("WRONG-CODE");
        }

        var refused = await device.SignInAsync(Code); // Even the right code, until the window passes

        Assert.Equal(StatusCodes.Status429TooManyRequests, refused.StatusCode);
        Assert.Contains("Too many attempts", refused.Body);
    }

    [Fact]
    public async Task ThisComputer_NeverNeedsToSignIn()
    {
        AccessCode.Set(Code);
        var device = new RemoteDevice(factory, "127.0.0.1");

        var admin = await device.GetAsync("/");

        Assert.Equal(StatusCodes.Status200OK, admin.StatusCode);
    }

    [GeneratedRegex("""name="(?<name>[^"]+)" value="(?<value>[^"]+)" """)]
    private static partial Regex HiddenField();

    private sealed record Response(int StatusCode, IHeaderDictionary Headers, string Body);

    /// <summary>
    /// Requests the test server as a device at one address, keeping its cookies like a browser.
    /// </summary>
    private sealed class RemoteDevice(RiftcasterWebApplicationFactory factory, string address)
    {
        private readonly Dictionary<string, string> _cookies = [];

        public Task<Response> GetAsync(string path) => SendAsync(HttpMethods.Get, path, body: null);

        public Task<Response> PostFormAsync(string path, Dictionary<string, string> fields) =>
            SendAsync(HttpMethods.Post, path, string.Join('&', fields.Select(field => $"{Uri.EscapeDataString(field.Key)}={Uri.EscapeDataString(field.Value)}")));

        /// <summary>
        /// Opens the sign-in page, then posts its form, with its token, and the code.
        /// </summary>
        public async Task<Response> SignInAsync(string code)
        {
            var page = await GetAsync("/signin");
            var token = HiddenField().Match(page.Body);
            Assert.True(token.Success, "The sign-in page has no form token.");

            return await PostFormAsync("/signin", new()
            {
                [WebUtility.HtmlDecode(token.Groups["name"].Value)] = WebUtility.HtmlDecode(token.Groups["value"].Value),
                ["code"] = code,
            });
        }

        private async Task<Response> SendAsync(string method, string path, string? body)
        {
            var context = await factory.Server.SendAsync(request =>
            {
                request.Request.Method = method;
                request.Request.Path = path;
                request.Connection.RemoteIpAddress = IPAddress.Parse(address);
                if (_cookies.Count > 0)
                {
                    request.Request.Headers.Cookie = string.Join("; ", _cookies.Select(cookie => $"{cookie.Key}={cookie.Value}"));
                }

                if (body is not null)
                {
                    request.Request.ContentType = "application/x-www-form-urlencoded";
                    request.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
                }
            });

            foreach (var setCookie in context.Response.Headers.SetCookie)
            {
                var pair = setCookie!.Split(';')[0].Split('=', 2);
                if (setCookie.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase))
                {
                    _cookies.Remove(pair[0]); // Cleared, e.g. by signing out
                }
                else
                {
                    _cookies[pair[0]] = pair[1];
                }
            }

            var text = await new StreamReader(context.Response.Body).ReadToEndAsync();
            return new Response(context.Response.StatusCode, context.Response.Headers, text);
        }
    }
}
