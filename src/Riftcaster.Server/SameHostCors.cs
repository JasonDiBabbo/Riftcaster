namespace Riftcaster.Server;

/// <summary>
/// Lets a page served by this server on another port read a response, so that a page of the web interface can
/// ask the new port who it is when the server moves there (see PortSwitcher).
/// </summary>
/// <remarks>
/// A page's origin includes its port, so the browser treats the same computer on another port as another site
/// and hides the answer unless it says otherwise. It says so only to a page from the same host: another website's
/// page, which could only be here by the DNS rebinding that HostCheck stops, or another computer's, is told nothing.
/// Only for answers that are no secret, such as who the server is.
/// </remarks>
internal static class SameHostCors
{
    /// <summary>
    /// Adds the header that lets a page from the same host, on any port, read the response.
    /// </summary>
    /// <param name="context">The request being answered.</param>
    public static void Allow(HttpContext context)
    {
        // Whichever origin asks, the answer depends on it: caches mustn't give one's to another.
        context.Response.Headers.Vary = "Origin";

        var origin = context.Request.Headers.Origin.ToString();
        if (Uri.TryCreate(origin, UriKind.Absolute, out var page)
            && (page.Scheme == Uri.UriSchemeHttp || page.Scheme == Uri.UriSchemeHttps)
            && string.Equals(page.Host, context.Request.Host.Host, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Headers.AccessControlAllowOrigin = origin;
        }
    }
}
