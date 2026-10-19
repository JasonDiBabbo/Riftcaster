using Microsoft.Extensions.FileProviders;
using Riftcaster.Core.Overlays;

namespace Riftcaster.Server;

public static class OverlayHostingExtensions
{
    /// <summary>
    /// Finds the built overlay pages, from the Overlays:Path setting, for <see cref="UseOverlays"/>
    /// and the Live dashboard's header.
    /// </summary>
    /// <remarks>
    /// A published build has them in its overlays folder (appsettings.json); Development uses the
    /// repository's src/overlays/dist, so watch rebuilds show up straight away
    /// (appsettings.Development.json).
    /// </remarks>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The web application builder.</returns>
    public static WebApplicationBuilder AddOverlays(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(OverlayFiles.Find(builder.Environment.ContentRootPath, builder.Configuration["Overlays:Path"]));
        return builder;
    }

    /// <summary>
    /// Serves the built overlay pages at /overlays, from the folder <see cref="AddOverlays"/> found.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application.</returns>
    public static WebApplication UseOverlays(this WebApplication app)
    {
        var files = app.Services.GetRequiredService<OverlayFiles>();

        if (files.Folder is null)
        {
            app.Logger.LogError("The Overlays:Path setting is missing. No overlays will be served. Your streaming software won't be able to show anything. Set it in appsettings.json.");
            return app;
        }

        if (!files.Found)
        {
            app.Logger.LogError("The overlays folder '{Path}' doesn't exist. No overlays will be served. Your streaming software won't be able to show anything. Build the server (or publish it) again to create the folder.", files.Folder);
            return app;
        }

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(files.Folder), // the folder on disk
            RequestPath = "/overlays",                       // the URL prefix
        });

        return app;
    }
}
