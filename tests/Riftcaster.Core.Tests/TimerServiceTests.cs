using Microsoft.Extensions.Time.Testing;
using Riftcaster.Contracts;
using Riftcaster.Core.Match;
using Riftcaster.Core.Timer;

namespace Riftcaster.Core.Tests;

public sealed class TimerServiceTests : IDisposable
{
    // A clock that only moves when a test calls Advance. Advance also fires any timers that come due.
    private readonly FakeTimeProvider _time = new();

    private readonly MatchService _match = new(new InMemoryMatchStore()); // 50 minutes, overtime allowed

    private readonly TimerService _service;

    private int _changedCount;

    public TimerServiceTests()
    {
        _service = new TimerService(_match, _time);
        _service.Changed += () => _changedCount++;
    }

    public void Dispose()
    {
        _service.Dispose();
    }

    [Fact]
    public void Constructor_StartsReadyAtTheMatchDuration()
    {
        Assert.Equal(new TimerState(50 * 60, 0, Running: false, AllowOvertime: true), _service.State);
    }

    [Fact]
    public void Start_CountsElapsedTime()
    {
        _service.Start();
        _time.Advance(TimeSpan.FromSeconds(10));

        Assert.True(_service.State.Running);
        Assert.Equal(10_000, _service.State.ElapsedMilliseconds);
        Assert.Equal(1, _changedCount); // Once for Start, not once per second
    }

    [Fact]
    public void Start_WhileRunning_DoesNothing()
    {
        _service.Start();
        _changedCount = 0;

        _service.Start();

        Assert.Equal(0, _changedCount);
    }

    [Fact]
    public void Pause_KeepsTheElapsedTime()
    {
        _service.Start();
        _time.Advance(TimeSpan.FromSeconds(10));

        _service.Pause();
        _time.Advance(TimeSpan.FromSeconds(5));

        Assert.False(_service.State.Running);
        Assert.Equal(10_000, _service.State.ElapsedMilliseconds);
    }

    [Fact]
    public void Start_AfterPause_ResumesFromTheElapsedTime()
    {
        _service.Start();
        _time.Advance(TimeSpan.FromSeconds(10));
        _service.Pause();
        _time.Advance(TimeSpan.FromSeconds(5));

        _service.Start();
        _time.Advance(TimeSpan.FromSeconds(3));

        Assert.Equal(13_000, _service.State.ElapsedMilliseconds);
    }

    [Fact]
    public void Pause_WhileStopped_DoesNothing()
    {
        _service.Pause();

        Assert.Equal(0, _changedCount);
    }

    [Fact]
    public void Reset_StopsClearsAndReturnsToTheMatchDuration()
    {
        _service.Adjust(60);
        _service.Start();
        _time.Advance(TimeSpan.FromSeconds(10));

        _service.Reset();

        Assert.Equal(new TimerState(50 * 60, 0, Running: false, AllowOvertime: true), _service.State);
    }

    [Theory]
    [InlineData(60, 51 * 60)]
    [InlineData(-10, 50 * 60 - 10)]
    [InlineData(-60 * 60, 0)]
    [InlineData(int.MaxValue, TimerService.MaxTotalSeconds)]
    public void Adjust_ChangesTheTotalWithinItsLimits(int seconds, int expected)
    {
        _service.Adjust(seconds);

        Assert.Equal(expected, _service.State.TotalSeconds);
    }

    [Fact]
    public void Adjust_KeepsTheElapsedTime()
    {
        _service.Start();
        _time.Advance(TimeSpan.FromSeconds(10));

        _service.Adjust(60);

        Assert.Equal(10_000, _service.State.ElapsedMilliseconds);
        Assert.True(_service.State.Running);
    }

    [Fact]
    public void SetRemaining_WhileRunning_RestartsFromTheNewTotal()
    {
        _service.Start();
        _time.Advance(TimeSpan.FromSeconds(10));

        _service.SetRemaining(5 * 60);
        _time.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(new TimerState(5 * 60, 2_000, Running: true, AllowOvertime: true), _service.State);
    }

    [Fact]
    public void SetRemaining_WhilePaused_StaysPaused()
    {
        _service.Start();
        _time.Advance(TimeSpan.FromSeconds(10));
        _service.Pause();

        _service.SetRemaining(5 * 60);

        Assert.Equal(new TimerState(5 * 60, 0, Running: false, AllowOvertime: true), _service.State);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(TimerService.MaxTotalSeconds + 1)]
    public void SetRemaining_OutOfRange_Throws(int seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _service.SetRemaining(seconds));
    }

    [Fact]
    public void ReachingZero_WithOvertime_KeepsCounting()
    {
        _service.SetRemaining(60);
        _service.Start();

        _time.Advance(TimeSpan.FromSeconds(75));

        Assert.True(_service.State.Running);
        Assert.Equal(75_000, _service.State.ElapsedMilliseconds);
    }

    [Fact]
    public void ReachingZero_WithoutOvertime_StopsAtZero()
    {
        _match.Update(settings => settings with { AllowOvertime = false });
        _service.SetRemaining(60);
        _service.Start();
        _changedCount = 0;

        _time.Advance(TimeSpan.FromSeconds(75));

        Assert.Equal(new TimerState(60, 60_000, Running: false, AllowOvertime: false), _service.State);
        Assert.Equal(1, _changedCount);
    }

    [Fact]
    public void Start_AfterRunningOutWithoutOvertime_DoesNothing()
    {
        _match.Update(settings => settings with { AllowOvertime = false });
        _service.SetRemaining(60);
        _service.Start();
        _time.Advance(TimeSpan.FromSeconds(60));
        _changedCount = 0;

        _service.Start();

        Assert.False(_service.State.Running);
        Assert.Equal(0, _changedCount);
    }

    [Fact]
    public void Adjust_BelowTheElapsedTimeWithoutOvertime_StopsAtZero()
    {
        _match.Update(settings => settings with { AllowOvertime = false });
        _service.Start();
        _time.Advance(TimeSpan.FromMinutes(10));

        _service.Adjust(-45 * 60); // Total 5:00, but 10:00 has run

        Assert.Equal(new TimerState(5 * 60, 5 * 60 * 1000, Running: false, AllowOvertime: false), _service.State);
    }

    [Fact]
    public void TurningOvertimeOff_InOvertime_StopsAtZero()
    {
        _service.SetRemaining(60);
        _service.Start();
        _time.Advance(TimeSpan.FromSeconds(75));

        _match.Update(settings => settings with { AllowOvertime = false });

        Assert.Equal(new TimerState(60, 60_000, Running: false, AllowOvertime: false), _service.State);
    }

    [Fact]
    public void DurationChanged_WhileUntouched_FollowsIt()
    {
        _match.Update(settings => settings with { DurationMinutes = 30 });

        Assert.Equal(30 * 60, _service.State.TotalSeconds);
        Assert.Equal(1, _changedCount);
    }

    [Fact]
    public void DurationChanged_AfterStarting_KeepsTheTotal()
    {
        _service.Start();
        _time.Advance(TimeSpan.FromSeconds(10));
        _service.Pause();

        _match.Update(settings => settings with { DurationMinutes = 30 });

        Assert.Equal(50 * 60, _service.State.TotalSeconds);
    }

    [Fact]
    public void MatchChanged_NothingAffectingTheTimer_DoesNotRaiseChanged()
    {
        _service.Start();
        _changedCount = 0;

        _match.Update(settings => settings with { PointsToWin = 12 });

        Assert.Equal(0, _changedCount);
    }

    [Fact]
    public async Task WatchAsync_YieldsTheCurrentStateFirst()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var watch = _service.WatchAsync(cts.Token).GetAsyncEnumerator();

        Assert.True(await watch.MoveNextAsync());
        Assert.Equal(_service.State, watch.Current);
    }

    [Fact]
    public async Task WatchAsync_YieldsChangesWithTheirElapsedTime()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var watch = _service.WatchAsync(cts.Token).GetAsyncEnumerator();
        Assert.True(await watch.MoveNextAsync()); // Consume the initial state
        _service.Start();
        Assert.True(await watch.MoveNextAsync()); // Consume the start
        _time.Advance(TimeSpan.FromSeconds(10));

        _service.Pause();

        Assert.True(await watch.MoveNextAsync());
        Assert.Equal(new TimerState(50 * 60, 10_000, Running: false, AllowOvertime: true), watch.Current);
    }

    [Fact]
    public async Task WatchAsync_EndsWhenCancelled()
    {
        using var cts = new CancellationTokenSource(); // no timeout: we cancel it ourselves
        await using var watch = _service.WatchAsync(cts.Token).GetAsyncEnumerator();
        Assert.True(await watch.MoveNextAsync());

        cts.Cancel();

        Assert.False(await watch.MoveNextAsync()); // Stream ended normally
    }
}
