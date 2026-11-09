using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Riftcaster.Core.Cards;

namespace Riftcaster.Core.Tests;

// RiftHunt asks users to be conservative, which the Settings page's confirmation and the background refresher see to:
// the source itself only keeps to a wait RiftHunt asks for (Retry-After). Nothing here reaches the network: the
// handler throws unless a test says what to answer.
public class RiftHuntCardSourceTests
{
    private const string OneCard = """{"cards":[{"id":"1","riftboundId":"tst-001-1","name":"Poppy, Paragon","type":"Unit","supertype":null,"domains":["Body"],"energy":4,"setName":"Origins","imgUrl":"https://cmsassets.rgpub.io/poppy.png","orientation":"portrait","tags":["Poppy"]}]}""";

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero));

    private readonly ThrowingHandler _handler = new();

    private RiftHuntCardSource Create() =>
        new(new HttpClient(_handler) { BaseAddress = new Uri("https://api.rifthunt.com/") }, _time, NullLogger<RiftHuntCardSource>.Instance);

    private static async Task<Exception> Failure(RiftHuntCardSource source) =>
        await Assert.ThrowsAnyAsync<Exception>(() => source.FetchAllAsync(null, CancellationToken.None));

    [Fact]
    public async Task AFetch_AsksForTheBulkFileOnce()
    {
        _handler.RespondWith(HttpStatusCode.OK, OneCard);

        var cards = await Create().FetchAllAsync(null, CancellationToken.None);

        Assert.Single(cards);
        Assert.Equal(["https://api.rifthunt.com/bulk/cards"], _handler.Requests.Select(uri => uri.ToString()));
    }

    // How often to ask is up to whoever asks: the person confirms a click, and the refresher keeps its own pace.
    [Fact]
    public async Task AFetchRightAfterAnother_IsNotRefused()
    {
        _handler.RespondWith(HttpStatusCode.OK, OneCard);
        var source = Create();

        await source.FetchAllAsync(null, CancellationToken.None);
        await source.FetchAllAsync(null, CancellationToken.None);

        Assert.Null(source.NotBefore);
        Assert.Equal(2, _handler.Requests.Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task ARetryAfter_IsWaitedFor_WithoutRequestingAnything(HttpStatusCode status)
    {
        _handler.RespondWith(status, retryAfter: TimeSpan.FromHours(5));
        var source = Create();

        await Failure(source);
        _time.Advance(TimeSpan.FromHours(1));
        var exception = await Assert.ThrowsAsync<CardFetchNotAllowedYetException>(() => source.FetchAllAsync(null, CancellationToken.None));

        Assert.Equal(_time.GetUtcNow() + TimeSpan.FromHours(4), source.NotBefore);
        Assert.Equal(source.NotBefore, exception.NotBefore);
        Assert.Single(_handler.Requests);
    }

    [Fact]
    public async Task ARetryAfterDate_IsWaitedFor()
    {
        _handler.RespondWith(HttpStatusCode.ServiceUnavailable, retryAfterDate: _time.GetUtcNow().AddHours(6));
        var source = Create();

        await Failure(source);

        Assert.Equal(_time.GetUtcNow() + TimeSpan.FromHours(6), source.NotBefore);
    }

    [Fact]
    public async Task WhenTheRetryAfterIsOver_ItMayBeAskedAgain()
    {
        _handler.RespondWith(HttpStatusCode.TooManyRequests, retryAfter: TimeSpan.FromHours(5));
        var source = Create();
        await Failure(source);

        _time.Advance(TimeSpan.FromHours(5));

        Assert.Null(source.NotBefore);
        _handler.RespondWith(HttpStatusCode.OK, OneCard);
        Assert.Single(await source.FetchAllAsync(null, CancellationToken.None));
    }

    // A number of days is more likely a mistake than a request.
    [Fact]
    public async Task AnAbsurdRetryAfter_IsCapped()
    {
        _handler.RespondWith(HttpStatusCode.TooManyRequests, retryAfter: TimeSpan.FromDays(400));
        var source = Create();

        await Failure(source);

        Assert.Equal(_time.GetUtcNow() + RiftHuntCardSource.LongestRetryAfter, source.NotBefore);
    }

    // Only a 429 or 503 asks for a wait; without a Retry-After there is nothing to wait for.
    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, false)]
    [InlineData(HttpStatusCode.ServiceUnavailable, false)]
    [InlineData(HttpStatusCode.InternalServerError, true)]
    [InlineData(HttpStatusCode.NotFound, true)]
    public async Task WithoutARetryAfterThatCounts_NothingIsWaitedFor(HttpStatusCode status, bool withRetryAfter)
    {
        _handler.RespondWith(status, retryAfter: withRetryAfter ? TimeSpan.FromHours(5) : null);
        var source = Create();

        var exception = await Failure(source);

        Assert.Equal(status, ((HttpRequestException)exception).StatusCode);
        Assert.Null(source.NotBefore);
    }

    // Kept in memory: a restart is a person's doing, and starts over.
    [Fact]
    public async Task ARetryAfter_IsForgottenByANewSource()
    {
        _handler.RespondWith(HttpStatusCode.TooManyRequests, retryAfter: TimeSpan.FromHours(5));
        await Failure(Create());

        Assert.Null(Create().NotBefore);
    }

    [Fact]
    public async Task NoAnswerAtAll_IsAFailureWithNoWait()
    {
        _handler.Respond = _ => throw new HttpRequestException("No such host is known.");
        var source = Create();

        var exception = await Failure(source);

        Assert.Null(((HttpRequestException)exception).StatusCode);
        Assert.Null(source.NotBefore);
        Assert.Single(_handler.Requests);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"cards":"no"}""")]
    [InlineData("""{"fetched":"2026-10-19T18:10:54.276Z"}""")]
    public async Task AnAnswerThatIsNotTheCardFile_IsAJsonFailure_AndIsNotRetried(string body)
    {
        _handler.RespondWith(HttpStatusCode.OK, body);
        var source = Create();

        await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(() => source.FetchAllAsync(null, CancellationToken.None));

        Assert.Single(_handler.Requests);
    }

    [Fact]
    public async Task ADownloadCutOffPartWay_IsAFailureTheCatalogUnderstands()
    {
        _handler.Respond = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new CutOffContent() });
        var source = Create();

        var exception = await Failure(source);

        Assert.IsType<HttpRequestException>(exception);
        Assert.Null(((HttpRequestException)exception).StatusCode);
    }

    [Fact]
    public async Task StoppedMidFetch_IsCanceled()
    {
        using var stop = new CancellationTokenSource();
        _handler.Respond = async _ =>
        {
            await stop.CancelAsync();
            stop.Token.ThrowIfCancellationRequested();
            return new HttpResponseMessage(HttpStatusCode.OK);
        };
        var source = Create();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.FetchAllAsync(null, stop.Token));
    }

    // A body whose read fails part way, as a dropped connection does.
    private sealed class CutOffContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => throw new IOException("The connection was closed.");

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override Task<Stream> CreateContentReadStreamAsync() => throw new IOException("The connection was closed.");
    }
}
