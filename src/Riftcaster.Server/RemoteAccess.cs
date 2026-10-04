using Riftcaster.Core.Network;
using Riftcaster.Server.Api;

namespace Riftcaster.Server;

/// <summary>
/// Limits what other devices on the network can reach (issue #16). Overlays need nothing: their
/// pages and what they read are open. The admin, which controls the broadcast, needs a sign-in
/// with the access code (see <see cref="OperatorSignIn"/>); with no code set, it's on this
/// computer only. The REST API (issue #9) takes the code in a header instead, from tools that
/// can't sign in: <c>Authorization: Bearer &lt;code&gt;</c>.
/// </summary>
/// <remarks>
/// Only matters while network access is on: while it's off, the server listens on localhost
/// alone, so every request comes from this computer. Runs after authentication, which reads and
/// checks the sign-in cookie.
/// </remarks>
internal static class RemoteAccess
{
    public static IApplicationBuilder UseRemoteAccessLimits(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (NetworkAccess.IsLocal(context.Connection.RemoteIpAddress)
                || (HttpMethods.IsGet(context.Request.Method) && IsOpenToTheNetwork(context.Request.Path)) // Overlays only read
                || IsPartOfSigningIn(context.Request.Path)
                || context.User.Identity?.IsAuthenticated == true)
            {
                await next(context);
                return;
            }

            var accessCode = context.RequestServices.GetRequiredService<AccessCode>();
            var isApi = RestApi.IsApiPath(context.Request.Path);
            if (!accessCode.IsSet)
            {
                const string OnlyHere = "The Riftcaster admin is only available on the computer running the server. To use it from this device, set an access code in the admin there.";
                if (isApi)
                {
                    await Problem(context, StatusCodes.Status403Forbidden, "No access code is set", OnlyHere);
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "text/plain; charset=utf-8";
                await context.Response.WriteAsync(OnlyHere);
                return;
            }

            // A page (the admin, or the API's docs, which a browser can't send the code to): send
            // the operator to sign in. Anything else (the admin's live connection, its scripts)
            // just fails until they have.
            if (HttpMethods.IsGet(context.Request.Method)
                && (context.Request.Path == "/" || context.Request.Path.StartsWithSegments(RestApi.DocsPath)))
            {
                context.Response.Redirect(OperatorSignIn.SignInPath);
                return;
            }

            if (isApi)
            {
                await CheckApiCodeAsync(context, accessCode, next);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        });

    /// <summary>
    /// Lets a REST API request through if it carries the access code, as
    /// <c>Authorization: Bearer &lt;code&gt;</c>, and answers it otherwise. Wrong codes are
    /// limited per device (see <see cref="WrongCodeAttempts"/>).
    /// </summary>
    private static async Task CheckApiCodeAsync(HttpContext context, AccessCode accessCode, RequestDelegate next)
    {
        if (BearerToken(context.Request) is not { } code)
        {
            context.Response.Headers.WWWAuthenticate = "Bearer";
            await Problem(context, StatusCodes.Status401Unauthorized, "The access code is needed",
                "From other devices, send the access code set in the admin, as Authorization: Bearer <code>.");
            return;
        }

        var attempts = context.RequestServices.GetRequiredService<WrongCodeAttempts>();
        var device = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (attempts.IsLockedOut(device))
        {
            await Problem(context, StatusCodes.Status429TooManyRequests, "Too many wrong codes",
                "Wait a minute, then try again with the right code.");
            return;
        }

        if (!accessCode.Matches(code))
        {
            attempts.CountWrongCode(device);
            context.Response.Headers.WWWAuthenticate = "Bearer error=\"invalid_token\"";
            await Problem(context, StatusCodes.Status401Unauthorized, "That access code isn't right",
                "Check it with whoever runs the server. It stops working when it's changed or removed.");
            return;
        }

        await next(context);
    }

    /// <summary>
    /// The token in an <c>Authorization: Bearer &lt;token&gt;</c> header, or null if there isn't one.
    /// </summary>
    internal static string? BearerToken(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        const string Scheme = "Bearer ";
        return header.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase) && header.Length > Scheme.Length
            ? header[Scheme.Length..].Trim()
            : null;
    }

    private static Task Problem(HttpContext context, int status, string title, string detail) =>
        Results.Problem(statusCode: status, title: title, detail: detail).ExecuteAsync(context);

    /// <summary>
    /// Whether other devices may request this path without signing in: what an overlay page loads
    /// and connects to (/overlays, the /api/{stream}/events streams, /api/info).
    /// </summary>
    internal static bool IsOpenToTheNetwork(PathString path)
    {
        if (path.StartsWithSegments("/overlays") || path.Equals("/api/info", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
        return segments is [var api, _, var events]
            && api.Equals("api", StringComparison.OrdinalIgnoreCase)
            && events.Equals("events", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Whether this path is part of signing in, which a device must reach before it has: the
    /// sign-in page and its post, sign-out, and the admin's font, which the page uses.
    /// </summary>
    internal static bool IsPartOfSigningIn(PathString path) =>
        path.Equals(OperatorSignIn.SignInPath, StringComparison.OrdinalIgnoreCase)
        || path.Equals(OperatorSignIn.SignOutPath, StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/_content/Riftcaster.Admin/fonts");
}
