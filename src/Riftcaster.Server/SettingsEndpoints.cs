using Microsoft.AspNetCore.Http.HttpResults;
using Riftcaster.Contracts;
using Riftcaster.Core.Settings;
using Riftcaster.Server.Api;

namespace Riftcaster.Server;

public static class SettingsEndpoints
{
    public static WebApplication MapSettings(this WebApplication app)
    {
        // REST (issue #9): the Settings page.
        var settings = app.MapGroup("/api/settings").WithTags("Settings");

        settings.MapGet("/", (AppSettingsService service) => TypedResults.Ok(service.Settings))
            .WithSummary("Get the settings");

        settings.MapPatch("/", Results<Ok<AppSettings>, ValidationProblem> (AppSettingsChange change, AppSettingsService service) =>
            {
                // The enum's JSON also accepts numbers, so a number that isn't one of them gets this far.
                if (change.Logging?.Detail is { } detail && !Enum.IsDefined(detail))
                {
                    return ApiProblems.Invalid("logging.detail", "Use WarningsOnly, Normal or Detailed.");
                }

                service.Update(current => current with
                {
                    Logging = current.Logging with { Detail = change.Logging?.Detail ?? current.Logging.Detail },
                });
                return TypedResults.Ok(service.Settings);
            })
            .WithSummary("Change the settings")
            .WithDescription("Only the fields sent change. A new log detail applies to the log at once.");

        return app;
    }
}

/// <summary>
/// Changes to the settings. Leave out a part or a field, or send null, to keep it as it is.
/// </summary>
/// <param name="Logging">Changes to the logging settings.</param>
public record AppSettingsChange(LoggingSettingsChange? Logging = null);

/// <summary>
/// Changes to the logging settings. Leave out a field, or send null, to keep it as it is.
/// </summary>
/// <param name="Detail">How much the server writes to its log: WarningsOnly, Normal or Detailed.</param>
public record LoggingSettingsChange(LogDetail? Detail = null);
