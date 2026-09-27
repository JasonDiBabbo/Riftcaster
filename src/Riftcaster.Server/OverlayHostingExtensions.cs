using Microsoft.Extensions.FileProviders;

namespace Riftcaster.Server;

public static class OverlayHostingExtensions
{
    /// <summary>
    /// Serves the built overlay pages at /overlays, from the configured Overlays:Path.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application.</returns>
    public static WebApplication UseOverlays(this WebApplication app)
    {
        string configKey = "Overlays:Path";
        string? overlaysRelativePath = app.Configuration[configKey];

        if (string.IsNullOrEmpty(overlaysRelativePath))
        {
            app.Logger.LogWarning("Overlays path is not configured; overlays will not be served.");
            return app;
        }

        string overlaysAbsolutePath = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, overlaysRelativePath));
        if (!Directory.Exists(overlaysAbsolutePath))
        {
            app.Logger.LogWarning("Overlays path '{Path}' does not exist; overlays will not be served.", overlaysAbsolutePath);
            return app;
        }
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(overlaysAbsolutePath), // the folder on disk
            RequestPath = "/overlays",                             // the URL prefix
        });

        return app;
    }
}
