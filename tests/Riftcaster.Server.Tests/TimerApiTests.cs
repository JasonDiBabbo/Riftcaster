using Riftcaster.Contracts;

namespace Riftcaster.Server.Tests;

// Its own test server (a fixture per class): these tests change the timer.
public class TimerApiTests(RiftcasterWebApplicationFactory factory) : IClassFixture<RiftcasterWebApplicationFactory>
{
    private readonly RestApiClient _api = new(factory.CreateClient());

    [Fact]
    public async Task StartPauseReset_ControlTheTimer()
    {
        var started = await _api.SendAsync<TimerState>(HttpMethod.Post, "/api/timer/start");
        Assert.True(started.Running);
        Assert.True((await _api.GetAsync<TimerState>("/api/timer")).Running);

        var paused = await _api.SendAsync<TimerState>(HttpMethod.Post, "/api/timer/pause");
        Assert.False(paused.Running);

        await _api.SendAsync<TimerState>(HttpMethod.Post, "/api/timer/adjust", new { seconds = 60 });
        var reset = await _api.SendAsync<TimerState>(HttpMethod.Post, "/api/timer/reset");
        Assert.False(reset.Running);
        Assert.Equal(0, reset.ElapsedMilliseconds);
        var match = await _api.GetAsync<MatchSettings>("/api/match");
        Assert.Equal(match.DurationMinutes * 60, reset.TotalSeconds);
    }

    [Fact]
    public async Task Adjust_ChangesTheLength()
    {
        var before = await _api.SendAsync<TimerState>(HttpMethod.Post, "/api/timer/reset");

        var after = await _api.SendAsync<TimerState>(HttpMethod.Post, "/api/timer/adjust", new { seconds = -60 });

        Assert.Equal(before.TotalSeconds - 60, after.TotalSeconds);
    }

    [Fact]
    public async Task SetRemaining_SetsTheTimeLeft()
    {
        var timer = await _api.SendAsync<TimerState>(HttpMethod.Put, "/api/timer/remaining", new { seconds = 90 });

        Assert.Equal(90, timer.TotalSeconds);
        Assert.Equal(0, timer.ElapsedMilliseconds);
    }

    [Theory]
    [InlineData("/api/timer/adjust", "{}", "The Seconds field is required.")]
    [InlineData("/api/timer/adjust", """{ "seconds": 60000 }""", "The field Seconds must be between -59999 and 59999.")]
    [InlineData("/api/timer/remaining", """{ "seconds": -1 }""", "The field Seconds must be between 0 and 59999.")]
    public async Task InvalidSeconds_Is400(string path, string body, string error)
    {
        var method = path.EndsWith("remaining") ? HttpMethod.Put : HttpMethod.Post;

        var errors = await _api.InvalidAsync(method, path, body);

        Assert.Equal([error], errors["seconds"]); // Named as in the JSON
    }
}
