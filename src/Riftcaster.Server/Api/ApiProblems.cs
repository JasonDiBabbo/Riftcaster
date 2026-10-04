using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Riftcaster.Server.Api;

/// <summary>
/// The REST API's error responses, as problem details (RFC 9457), like the ones ASP.NET writes for
/// a request it can't read.
/// </summary>
internal static class ApiProblems
{
    /// <summary>
    /// A 404 for an id or seat that doesn't exist.
    /// </summary>
    /// <param name="detail">What wasn't found, for example "No player in seat 5. Seats are 1 to 4."</param>
    public static NotFound<ProblemDetails> NotFound(string detail) =>
        TypedResults.NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Not found", Detail = detail });

    /// <summary>
    /// A 400 for one invalid field, in the same shape as the validation of request bodies.
    /// </summary>
    /// <param name="field">The field's JSON name, for example "points".</param>
    /// <param name="error">What's wrong with it.</param>
    public static ValidationProblem Invalid(string field, string error) =>
        Invalid(new Dictionary<string, string[]> { [field] = [error] });

    /// <summary>
    /// A 400 for one or more invalid fields.
    /// </summary>
    /// <param name="errors">Each invalid field's JSON name, and what's wrong with it.</param>
    public static ValidationProblem Invalid(IDictionary<string, string[]> errors) =>
        TypedResults.ValidationProblem(errors);
}
