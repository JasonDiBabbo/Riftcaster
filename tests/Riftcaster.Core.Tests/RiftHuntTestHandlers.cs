using System.Net;
using System.Net.Http.Headers;

namespace Riftcaster.Core.Tests;

/// <summary>
/// Answers every request with the same JSON. Never reaches the network.
/// </summary>
internal sealed class Answers(string json) : HttpMessageHandler
{
    public List<Uri> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
    }
}

/// <summary>
/// Throws for any request, so a test that reaches the network (which the real RiftHunt is) fails instead of asking it.
/// A test says what to answer with <see cref="Respond"/>.
/// </summary>
internal sealed class ThrowingHandler : HttpMessageHandler
{
    private readonly object _lock = new();

    private Func<HttpRequestMessage, Task<HttpResponseMessage>>? _respond;

    public List<Uri> Requests { get; } = [];

    /// <summary>
    /// How to answer. Without it, a request throws.
    /// </summary>
    public Func<HttpRequestMessage, Task<HttpResponseMessage>>? Respond
    {
        set
        {
            lock (_lock)
            {
                _respond = value;
            }
        }
    }

    public void RespondWith(HttpStatusCode status, string body = "", TimeSpan? retryAfter = null, DateTimeOffset? retryAfterDate = null) =>
        Respond = _ =>
        {
            var response = new HttpResponseMessage(status) { Content = new StringContent(body), ReasonPhrase = status.ToString() };
            if (retryAfter is { } delta)
            {
                response.Headers.RetryAfter = new RetryConditionHeaderValue(delta);
            }
            else if (retryAfterDate is { } date)
            {
                response.Headers.RetryAfter = new RetryConditionHeaderValue(date);
            }

            return Task.FromResult(response);
        };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Func<HttpRequestMessage, Task<HttpResponseMessage>>? respond;
        lock (_lock)
        {
            Requests.Add(request.RequestUri!);
            respond = _respond;
        }

        return respond is null
            ? throw new InvalidOperationException($"The test reached the network: {request.Method} {request.RequestUri}")
            : respond(request);
    }
}
