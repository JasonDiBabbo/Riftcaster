using Riftcaster.Core.Network;

namespace Riftcaster.Server;

/// <summary>
/// Limits what other devices on the network can reach (issue #16). Overlays need nothing: their
/// pages and what they read are open. The admin, which controls the broadcast, needs a sign-in
/// with the access code (see <see cref="OperatorSignIn"/>); with no code set, it's on this
/// computer only.
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
                || IsOpenToTheNetwork(context.Request.Path)
                || IsPartOfSigningIn(context.Request.Path)
                || context.User.Identity?.IsAuthenticated == true)
            {
                await next(context);
                return;
            }

            if (!context.RequestServices.GetRequiredService<AccessCode>().IsSet)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "text/plain; charset=utf-8";
                await context.Response.WriteAsync("The Riftcaster admin is only available on the computer running the server. To use it from this device, set an access code in the admin there.");
                return;
            }

            // A page: send the operator to sign in. Anything else (the admin's live connection,
            // its scripts) just fails until they have.
            if (HttpMethods.IsGet(context.Request.Method) && context.Request.Path == "/")
            {
                context.Response.Redirect(OperatorSignIn.SignInPath);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        });

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
