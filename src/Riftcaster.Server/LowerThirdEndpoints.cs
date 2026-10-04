using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Riftcaster.Contracts;
using Riftcaster.Core.LowerThird;
using Riftcaster.Server.Api;

namespace Riftcaster.Server;

public static class LowerThirdEndpoints
{
    public static WebApplication MapLowerThird(this WebApplication app)
    {
        app.MapGet("/api/lower-third/events",
            (HttpContext context, LowerThirdService lowerThird, IHostApplicationLifetime lifetime) =>
                StateSockets.SendStatesAsync(context, lowerThird.WatchAsync, lifetime.ApplicationStopping))
            .ExcludeFromDescription(); // A WebSocket, which OpenAPI can't describe

        // REST (issue #9): the admin's Lower thirds panel. The library holds saved lower thirds, by
        // id; showing one puts it on air.
        var lowerThird = app.MapGroup("/api/lower-third").WithTags("Lower thirds");

        lowerThird.MapGet("/", (LowerThirdService service) => TypedResults.Ok(LibraryOf(service)))
            .WithSummary("Get the library")
            .WithDescription("Every saved lower third, newest first, and which one is on air.");

        lowerThird.MapPost("/messages", async Task<Results<Created<LowerThirdEntry>, ValidationProblem>> (HttpRequest request, LowerThirdService service) =>
            {
                var (message, problem) = await ReadMessageAsync(request);
                if (problem is not null)
                {
                    return problem;
                }

                var entry = service.Add(message!);
                return TypedResults.Created($"/api/lower-third/messages/{entry.Id}", entry);
            })
            .Accepts<LowerThirdMessage>("application/json")
            .WithSummary("Save a new lower third")
            .WithDescription("""
                Adds it to the library, without showing it. The "type" field says which kind it is:
                - keyword: { "type": "keyword", "keyword": "Deflect", "description": "..." }
                - information: { "type": "information", "message": "..." }
                - socials: { "type": "socials", "links": [{ "network": "Twitch", "handle": "riftcaster" }] }

                Text is trimmed, and socials links with no handle are dropped.
                """);

        lowerThird.MapGet("/messages/{id:guid}", Results<Ok<LowerThirdEntry>, NotFound<ProblemDetails>> (Guid id, LowerThirdService service) =>
                service.Library.Entries.Find(entry => entry.Id == id) is { } entry ? TypedResults.Ok(entry) : NoEntry(id))
            .WithSummary("Get a saved lower third");

        lowerThird.MapPut("/messages/{id:guid}", async Task<Results<Ok<LowerThirdEntry>, NotFound<ProblemDetails>, ValidationProblem>> (
                Guid id, HttpRequest request, LowerThirdService service) =>
            {
                var (message, problem) = await ReadMessageAsync(request);
                if (problem is not null)
                {
                    return problem;
                }

                return service.Update(id, message!) ? TypedResults.Ok(new LowerThirdEntry(id, message!)) : NoEntry(id);
            })
            .Accepts<LowerThirdMessage>("application/json")
            .WithSummary("Replace a saved lower third")
            .WithDescription("The whole message, as for a new one; its kind can change too. If it's on air, the overlay shows the new version straight away.");

        lowerThird.MapDelete("/messages/{id:guid}", Results<NoContent, NotFound<ProblemDetails>> (Guid id, LowerThirdService service) =>
                service.Delete(id) ? TypedResults.NoContent() : NoEntry(id))
            .WithSummary("Delete a saved lower third")
            .WithDescription("If it's on air, it's taken off.");

        lowerThird.MapPost("/messages/{id:guid}/show", Results<Ok<LowerThirdLibraryView>, NotFound<ProblemDetails>> (Guid id, LowerThirdService service) =>
                service.Show(id) ? TypedResults.Ok(LibraryOf(service)) : NoEntry(id))
            .WithSummary("Show a saved lower third")
            .WithDescription("Puts it on air, in place of any lower third showing already.");

        lowerThird.MapPost("/hide", (LowerThirdService service) =>
            {
                service.Hide();
                return TypedResults.Ok(LibraryOf(service));
            })
            .WithSummary("Hide the lower third")
            .WithDescription("Takes whatever's on air off. Does nothing if nothing is showing.");

        return app;
    }

    private const string TypeNeeded = "Every lower third has a \"type\": keyword, information or socials.";

    /// <summary>
    /// Reads a lower third from the request's JSON, tidies it, and checks it.
    /// </summary>
    /// <remarks>
    /// Read here rather than bound as a parameter: JSON with no "type" can't be read as the abstract
    /// LowerThirdMessage, and ASP.NET answers that with a 500 rather than a 400.
    /// </remarks>
    /// <returns>The tidied message, or the 400 to send instead.</returns>
    private static async Task<(LowerThirdMessage? Message, ValidationProblem? Problem)> ReadMessageAsync(HttpRequest request)
    {
        LowerThirdMessage? message;
        try
        {
            var options = request.HttpContext.RequestServices.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value.SerializerOptions;
            message = request.HasJsonContentType()
                ? await request.ReadFromJsonAsync<LowerThirdMessage>(options, request.HttpContext.RequestAborted)
                : null;
        }
        catch (NotSupportedException)
        {
            return (null, ApiProblems.Invalid("type", TypeNeeded));
        }
        catch (JsonException exception)
        {
            return (null, ApiProblems.Invalid("body", $"Not a lower third: {exception.Message} {TypeNeeded}"));
        }

        if (message is null)
        {
            return (null, ApiProblems.Invalid("body", $"Send a lower third as JSON (Content-Type: application/json). {TypeNeeded}"));
        }

        var tidied = LowerThirdMessageRules.Normalize(message, out var errors);
        return errors.Count > 0 ? (null, ApiProblems.Invalid(errors.ToDictionary())) : (tidied, null);
    }

    private static LowerThirdLibraryView LibraryOf(LowerThirdService service) =>
        new(service.Library.Entries, service.Library.LiveEntryId);

    private static NotFound<ProblemDetails> NoEntry(Guid id) =>
        ApiProblems.NotFound($"No saved lower third with id {id}.");
}

/// <summary>
/// The lower third library.
/// </summary>
/// <param name="Entries">Every saved lower third, newest first.</param>
/// <param name="LiveEntryId">The id of the one on air, or null when nothing is showing.</param>
public record LowerThirdLibraryView(IReadOnlyList<LowerThirdEntry> Entries, Guid? LiveEntryId);
