namespace Riftcaster.Server.Api;

/// <summary>
/// Refuses REST API changes made by other websites' pages (issue #9).
/// </summary>
/// <remarks>
/// The API needs no sign-in from this computer, so without this any web page open in a browser
/// here could press its buttons: a hidden <c>fetch("http://localhost:5062/api/timer/reset", { method: "POST" })</c>
/// works without any CORS permission, since the browser sends it and only hides the response.
/// Browsers send an <c>Origin</c> header with such requests, saying which site made them; the
/// admin's own pages (the docs page included) are this server's origin, and tools such as Stream
/// Deck, Companion or curl send none. Reads are left alone: the browser doesn't let another site
/// see their responses.
/// </remarks>
internal static class CrossSiteRequests
{
    public static IApplicationBuilder UseCrossSiteRequestLimits(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (IsRefused(context.Request))
            {
                await Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Requests from other websites are refused",
                    detail: "The Riftcaster API only takes changes from tools such as Stream Deck, Companion or scripts, and from the server's own pages.")
                    .ExecuteAsync(context);
                return;
            }

            await next(context);
        });

    /// <summary>
    /// Whether this is a change to the API made by another website's page.
    /// </summary>
    internal static bool IsRefused(HttpRequest request)
    {
        if (!RestApi.IsApiPath(request.Path)
            || HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method))
        {
            return false;
        }

        var origin = request.Headers.Origin.ToString();
        return origin.Length > 0
            && !string.Equals(origin, $"{request.Scheme}://{request.Host}", StringComparison.OrdinalIgnoreCase);
    }
}
