using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace Riftcaster.Server.Tests;

/// <summary>
/// Calls the REST API on a test server, from this computer (the in-memory client has no remote
/// address, which counts as local), and reads its answers.
/// </summary>
internal sealed class RestApiClient(HttpClient client)
{
    public async Task<T> GetAsync<T>(string path) =>
        await ExpectAsync<T>(await client.GetAsync(path), HttpStatusCode.OK);

    /// <summary>
    /// Sends a request and expects a 200 with a body.
    /// </summary>
    public async Task<T> SendAsync<T>(HttpMethod method, string path, object? body = null) =>
        await ExpectAsync<T>(await SendRawAsync(method, path, body), HttpStatusCode.OK);

    /// <summary>
    /// Sends a request and returns the response, whatever its status.
    /// </summary>
    public Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string path, object? body = null) =>
        client.SendAsync(new HttpRequestMessage(method, path)
        {
            Content = body switch
            {
                null => null,
                string json => new StringContent(json, Encoding.UTF8, "application/json"), // Exactly as written
                HttpContent content => content,
                _ => JsonContent.Create(body),
            },
        });

    /// <summary>
    /// Sends a request and expects a 400 with validation errors, which it returns by field.
    /// </summary>
    public async Task<IDictionary<string, string[]>> InvalidAsync(HttpMethod method, string path, object? body = null)
    {
        var response = await SendRawAsync(method, path, body);
        var problem = await ExpectAsync<ValidationProblem>(response, HttpStatusCode.BadRequest);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return problem.Errors;
    }

    /// <summary>
    /// Sends a request and expects a 404 problem, whose detail it returns.
    /// </summary>
    public async Task<string> NotFoundAsync(HttpMethod method, string path, object? body = null)
    {
        var problem = await ExpectAsync<Problem>(await SendRawAsync(method, path, body), HttpStatusCode.NotFound);
        return problem.Detail;
    }

    private static async Task<T> ExpectAsync<T>(HttpResponseMessage response, HttpStatusCode status)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"Expected {(int)status}, got {(int)response.StatusCode}: {text}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    public sealed record Problem(int Status, string Title, string Detail);

    public sealed record ValidationProblem(int Status, Dictionary<string, string[]> Errors);
}
