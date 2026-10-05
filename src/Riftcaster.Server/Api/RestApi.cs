using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.OpenApi;
using Riftcaster.Contracts;
using Scalar.AspNetCore;

namespace Riftcaster.Server.Api;

/// <summary>
/// The REST API (issue #9): HTTP endpoints for tools outside the admin, such as Stream Deck or
/// Bitfocus Companion buttons and scripts. Each area's endpoints are in its own file (TimerEndpoints
/// and so on), next to its overlay stream, and call the same Core service methods as the admin.
/// </summary>
/// <remarks>
/// This computer needs nothing to call it. Other devices send the access code, as
/// <c>Authorization: Bearer &lt;code&gt;</c> (see <see cref="RemoteAccess"/>). Requests from other
/// websites are refused (see <see cref="CrossSiteRequests"/>).
/// </remarks>
internal static class RestApi
{
    /// <summary>
    /// The OpenAPI document, which describes every endpoint.
    /// </summary>
    public const string DocumentPath = "/openapi/v1.json";

    /// <summary>
    /// The Scalar page that shows the document, where endpoints can be tried out.
    /// </summary>
    public const string DocsPath = "/api/docs";

    /// <summary>
    /// Registers what the REST API needs: validation of request bodies (their DataAnnotations
    /// attributes, answered with a 400), problem details for errors, and the OpenAPI document.
    /// </summary>
    public static WebApplicationBuilder AddRestApi(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<WrongCodeAttempts>();
        builder.Services.AddValidation();
        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            // Validation names fields as in C# (Seconds, Links[0].Handle); name them as in the
            // JSON instead (seconds, links[0].handle).
            if (context.ProblemDetails is HttpValidationProblemDetails validation)
            {
                validation.Errors = validation.Errors.ToDictionary(error => JsonFieldName(error.Key), error => error.Value);
            }
        });

        // A lower third's "type" can come anywhere in its JSON, not only first, as a hand-written
        // request might have it.
        builder.Services.Configure<JsonOptions>(options => options.SerializerOptions.AllowOutOfOrderMetadataProperties = true);

        builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, context, _) =>
        {
            var identity = context.ApplicationServices.GetRequiredService<ServerIdentity>();
            document.Info = new OpenApiInfo
            {
                Title = "Riftcaster API",
                Version = identity.Version,
                Description = """
                    Control the overlays from outside the admin dashboard: Stream Deck or Bitfocus Companion buttons, scripts and so on. Everything the dashboard does is here, except network access and the access code, which can only be changed on the computer running the server.

                    **Calling it:** from the computer running the server, no sign-in is needed. From another device, network access must be on, and each request needs the access code, as `Authorization: Bearer <code>`.

                    Changes return the new state, so a button can show it. Invalid requests get a 400 with the details, and unknown seats and ids a 404.
                    """,
            };

            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes["accessCode"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                Description = "The access code set in the admin dashboard. Only needed from other devices.",
            };

            return Task.CompletedTask;
        }));

        return builder;
    }

    /// <summary>
    /// Whether a path is part of the REST API or its OpenAPI document, where other devices may
    /// send the access code in a header rather than signing in.
    /// </summary>
    internal static bool IsApiPath(PathString path) =>
        path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/openapi", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A C# member path as JSON names it, by the camelCase naming policy the API's JSON uses:
    /// "Links[0].Handle" becomes "links[0].handle".
    /// </summary>
    internal static string JsonFieldName(string memberPath) =>
        string.Join('.', memberPath.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));

    /// <summary>
    /// Maps the OpenAPI document and the Scalar page that shows it.
    /// </summary>
    public static WebApplication MapRestApiDocs(this WebApplication app)
    {
        app.MapOpenApi(DocumentPath);
        app.MapScalarApiReference(DocsPath, options => options
            .WithTitle("Riftcaster API")
            .WithOpenApiRoutePattern(DocumentPath)
            // Nothing the page does leaves this computer: Scalar's fonts come from its servers, its
            // AI chat and MCP setup send the API's description to its service, and its telemetry
            // counts each request tried here.
            .DisableDefaultFonts()
            .DisableAgent()
            .DisableMcp()
            .DisableTelemetry());

        return app;
    }
}
