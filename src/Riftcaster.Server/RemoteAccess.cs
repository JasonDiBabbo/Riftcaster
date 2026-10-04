using System.Net;

namespace Riftcaster.Server;

/// <summary>
/// Limits what other devices on the network can reach: the overlays and what they read, nothing
/// else. The admin, which controls the broadcast, stays on this computer only (issue #16).
/// </summary>
/// <remarks>
/// Only matters while network access is on: while it's off, the server listens on localhost
/// alone, so every request comes from this computer.
/// </remarks>
internal static class RemoteAccess
{
    /// <summary>
    /// Answers requests from other devices with 403 Forbidden, unless they're for something an
    /// overlay needs: its page and files (/overlays), its state streams (/api/…/events), and
    /// /api/info.
    /// </summary>
    public static IApplicationBuilder UseRemoteAccessLimits(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (IsLocal(context.Connection.RemoteIpAddress) || IsOpenToTheNetwork(context.Request.Path))
            {
                await next(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync("The Riftcaster admin is only available on the computer running the server.");
        });

    /// <summary>
    /// Whether a request came from this computer. In-memory test requests have no address, and count as local.
    /// </summary>
    internal static bool IsLocal(IPAddress? address) =>
        address is null || IPAddress.IsLoopback(address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address);

    /// <summary>
    /// Whether other devices may request this path: what an overlay page loads and connects to.
    /// </summary>
    internal static bool IsOpenToTheNetwork(PathString path)
    {
        if (path.StartsWithSegments("/overlays") || path.Equals("/api/info", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // The overlays' state streams: /api/{stream}/events.
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
        return segments is [var api, _, var events]
            && api.Equals("api", StringComparison.OrdinalIgnoreCase)
            && events.Equals("events", StringComparison.OrdinalIgnoreCase);
    }
}
