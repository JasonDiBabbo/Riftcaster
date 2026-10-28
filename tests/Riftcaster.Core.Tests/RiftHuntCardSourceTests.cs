using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Riftcaster.Contracts;
using Riftcaster.Core.Cards;

namespace Riftcaster.Core.Tests;

// RiftHunt may be asked for its catalog about once a day, and says so: these are the rules that keep to that. Nothing here
// reaches the network: the handler throws unless a test says what to answer.
public class RiftHuntCardSourceTests
{
    private const string OneCard = """{"cards":[{"id":"1","riftboundId":"tst-001-1","name":"Poppy, Paragon","type":"Unit","supertype":null,"domains":["Body"],"energy":4,"setName":"Origins","imgUrl":"https://cmsassets.rgpub.io/poppy.png","orientation":"portrait","tags":["Poppy"]}]}""";

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero));

    private readonly ThrowingHandler _handler = new();

    private readonly InMemoryCardFetchHistory _history = new();

    private RiftHuntCardSource Create(ICardFetchHistory? history = null) =>
        new(new HttpClient(_handler) { BaseAddress = new Uri("https://api.rifthunt.com/") }, history ?? _history, _time, NullLogger<RiftHuntCardSource>.Instance);

    private CardFetchRecord Remembered => _history.Get(CardSourceKind.RiftHunt);

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

    [Fact]
    public async Task AfterAFetch_ItWaitsADay()
    {
        _handler.RespondWith(HttpStatusCode.OK, OneCard);
        var source = Create();

        await source.FetchAllAsync(null, CancellationToken.None);

        Assert.Equal(_time.GetUtcNow() + RiftHuntCardSource.Cooldown, source.NotBefore);
        Assert.Equal(_time.GetUtcNow() + RiftHuntCardSource.Cooldown, Remembered.NotBefore);
        Assert.Equal(_time.GetUtcNow(), Remembered.LastAttemptAt);
        Assert.Equal(0, Remembered.Failures);
    }

    // Enforced by the source itself, not left to whoever asks: nothing is requested.
    [Fact]
    public async Task AskedAgainTooSoon_RefusesWithoutRequestingAnything()
    {
        _handler.RespondWith(HttpStatusCode.OK, OneCard);
        var source = Create();
        await source.FetchAllAsync(null, CancellationToken.None);
        _time.Advance(TimeSpan.FromHours(23));

        var exception = await Assert.ThrowsAsync<CardFetchNotAllowedYetException>(() => source.FetchAllAsync(null, CancellationToken.None));

        Assert.Single(_handler.Requests);
        Assert.Equal(_time.GetUtcNow() + TimeSpan.FromHours(1), exception.NotBefore);
    }

    [Fact]
    public async Task AfterTheDay_ItMayBeAskedAgain()
    {
        _handler.RespondWith(HttpStatusCode.OK, OneCard);
        var source = Create();
        await source.FetchAllAsync(null, CancellationToken.None);

        _time.Advance(RiftHuntCardSource.Cooldown);

        Assert.Null(source.NotBefore);
        await source.FetchAllAsync(null, CancellationToken.None);
        Assert.Equal(2, _handler.Requests.Count);
    }

    [Fact]
    public async Task Restarting_DoesNotSkipTheWait()
    {
        using var directory = new TempDirectory();
        var file = Path.Combine(directory.Path, "cardSources.json");
        _handler.RespondWith(HttpStatusCode.OK, OneCard);
        await Create(new JsonFileCardFetchHistory(file, NullLogger<JsonFileCardFetchHistory>.Instance)).FetchAllAsync(null, CancellationToken.None);

        // A new copy of Riftcaster: its history is read from the file.
        var restarted = Create(new JsonFileCardFetchHistory(file, NullLogger<JsonFileCardFetchHistory>.Instance));

        Assert.Equal(_time.GetUtcNow() + RiftHuntCardSource.Cooldown, restarted.NotBefore);
        await Assert.ThrowsAsync<CardFetchNotAllowedYetException>(() => restarted.FetchAllAsync(null, CancellationToken.None));
        Assert.Single(_handler.Requests);
    }

    [Fact]
    public void NothingFetchedYet_MayBeAskedAtOnce()
    {
        Assert.Null(Create().NotBefore);
    }

    // So that a crash, or Riftcaster being stopped, during the request still counts as an attempt.
    [Fact]
    public async Task TheAttemptIsNotedBeforeTheRequestGoesOut()
    {
        CardFetchRecord? during = null;
        _handler.Respond = _ =>
        {
            during = Remembered;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(OneCard) });
        };

        await Create().FetchAllAsync(null, CancellationToken.None);

        Assert.Equal(_time.GetUtcNow(), during!.LastAttemptAt);
        Assert.Equal(_time.GetUtcNow() + RiftHuntCardSource.FirstBackoff, during.NotBefore);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 8)]
    [InlineData(5, 16)]
    [InlineData(6, 24)]
    [InlineData(9, 24)]
    public async Task AServerError_WaitsAnHourThenDoubles_ToADay(int failures, int expectedHours)
    {
        _handler.RespondWith(HttpStatusCode.InternalServerError);
        var source = Create();

        for (var failure = 1; failure <= failures; failure++)
        {
            _time.Advance(TimeSpan.FromDays(2)); // Past any wait
            var exception = await Failure(source);
            Assert.Equal(HttpStatusCode.InternalServerError, ((HttpRequestException)exception).StatusCode);
        }

        Assert.Equal(_time.GetUtcNow() + TimeSpan.FromHours(expectedHours), Remembered.NotBefore);
        Assert.Equal(failures, Remembered.Failures);
        Assert.Equal(failures, _handler.Requests.Count);
    }

    [Fact]
    public async Task AFailed_IsNotTriedAgainAtOnce()
    {
        _handler.RespondWith(HttpStatusCode.BadGateway);
        var source = Create();
        await Failure(source);

        var exception = await Failure(source);

        Assert.IsType<CardFetchNotAllowedYetException>(exception);
        Assert.Single(_handler.Requests);
    }

    [Fact]
    public async Task AWorkingFetch_ClearsTheFailures()
    {
        _handler.RespondWith(HttpStatusCode.InternalServerError);
        var source = Create();
        await Failure(source);
        _time.Advance(TimeSpan.FromDays(1));
        await Failure(source);
        _time.Advance(TimeSpan.FromDays(1));
        _handler.RespondWith(HttpStatusCode.OK, OneCard);
        await source.FetchAllAsync(null, CancellationToken.None);

        Assert.Equal(0, Remembered.Failures);

        _time.Advance(RiftHuntCardSource.Cooldown);
        _handler.RespondWith(HttpStatusCode.ServiceUnavailable);
        await Failure(source);
        Assert.Equal(_time.GetUtcNow() + RiftHuntCardSource.FirstBackoff, Remembered.NotBefore); // An hour again, not four
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task ARetryAfterLongerThanTheBackoff_IsWaitedFor(HttpStatusCode status)
    {
        _handler.RespondWith(status, retryAfter: TimeSpan.FromHours(5));
        var source = Create();

        await Failure(source);

        Assert.Equal(_time.GetUtcNow() + TimeSpan.FromHours(5), Remembered.NotBefore);
    }

    [Fact]
    public async Task ARetryAfterDate_IsWaitedFor()
    {
        _handler.RespondWith(HttpStatusCode.ServiceUnavailable, retryAfterDate: _time.GetUtcNow().AddHours(6));
        var source = Create();

        await Failure(source);

        Assert.Equal(_time.GetUtcNow() + TimeSpan.FromHours(6), Remembered.NotBefore);
    }

    [Fact]
    public async Task ARetryAfterShorterThanTheBackoff_DoesNotShortenIt()
    {
        _handler.RespondWith(HttpStatusCode.TooManyRequests, retryAfter: TimeSpan.FromSeconds(30));
        var source = Create();

        await Failure(source);

        Assert.Equal(_time.GetUtcNow() + RiftHuntCardSource.FirstBackoff, Remembered.NotBefore);
    }

    // A number of days is more likely a mistake than a request.
    [Fact]
    public async Task AnAbsurdRetryAfter_IsCapped()
    {
        _handler.RespondWith(HttpStatusCode.TooManyRequests, retryAfter: TimeSpan.FromDays(400));
        var source = Create();

        await Failure(source);

        Assert.Equal(_time.GetUtcNow() + RiftHuntCardSource.LongestRetryAfter, Remembered.NotBefore);
    }

    // The same request would be refused again, so it isn't asked again for a day.
    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Gone)]
    public async Task ARefusedRequest_WaitsADay_AndIsNotRetried(HttpStatusCode status)
    {
        _handler.RespondWith(status);
        var source = Create();

        var exception = await Failure(source);

        Assert.Equal(status, ((HttpRequestException)exception).StatusCode);
        Assert.Equal(_time.GetUtcNow() + RiftHuntCardSource.Cooldown, Remembered.NotBefore);
        Assert.Single(_handler.Requests); // Not once more within the same fetch
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"cards":"no"}""")]
    [InlineData("""{"fetched":"2026-10-19T18:10:54.276Z"}""")]
    public async Task AnAnswerThatIsNotTheCardFile_WaitsADay_AndIsNotRetried(string body)
    {
        _handler.RespondWith(HttpStatusCode.OK, body);
        var source = Create();

        await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(() => source.FetchAllAsync(null, CancellationToken.None));

        Assert.Equal(_time.GetUtcNow() + RiftHuntCardSource.Cooldown, Remembered.NotBefore);
        Assert.Single(_handler.Requests);
    }

    [Fact]
    public async Task NoAnswerAtAll_WaitsAnHour()
    {
        _handler.Respond = _ => throw new HttpRequestException("No such host is known.");
        var source = Create();

        var exception = await Failure(source);

        Assert.Null(((HttpRequestException)exception).StatusCode);
        Assert.Equal(_time.GetUtcNow() + RiftHuntCardSource.FirstBackoff, Remembered.NotBefore);
        Assert.Single(_handler.Requests);
    }

    [Fact]
    public async Task ADownloadCutOffPartWay_IsAFailureTheCatalogUnderstands()
    {
        _handler.Respond = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new CutOffContent() });
        var source = Create();

        var exception = await Failure(source);

        Assert.IsType<HttpRequestException>(exception);
        Assert.Equal(_time.GetUtcNow() + RiftHuntCardSource.FirstBackoff, Remembered.NotBefore);
    }

    [Fact]
    public async Task ATimeout_CountsAsAnAttempt()
    {
        _handler.Respond = _ => throw new TaskCanceledException("The request timed out.");
        var source = Create();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.FetchAllAsync(null, CancellationToken.None));

        Assert.Equal(_time.GetUtcNow() + RiftHuntCardSource.FirstBackoff, Remembered.NotBefore);
    }

    [Fact]
    public async Task StoppedMidFetch_CountsAsAnAttempt()
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

        Assert.Equal(_time.GetUtcNow() + RiftHuntCardSource.FirstBackoff, Remembered.NotBefore);
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

    private sealed class TempDirectory : IDisposable
    {
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("riftcaster-tests-");

        public string Path => _directory.FullName;

        public void Dispose() => _directory.Delete(recursive: true);
    }
}
