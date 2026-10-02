using Riftcaster.Contracts;
using Riftcaster.Core.Timer;

namespace Riftcaster.Core.Tests;

public class TimerReadoutTests
{
    private const int Total = 50 * 60;

    [Theory]
    [InlineData(0, false, true, TimerStatus.Ready)]
    [InlineData(1_000, true, true, TimerStatus.Running)]
    [InlineData(1_000, false, true, TimerStatus.Paused)]
    [InlineData(Total * 1000 + 1, true, true, TimerStatus.Overtime)]
    [InlineData(Total * 1000 + 1, false, true, TimerStatus.OvertimePaused)]
    [InlineData(Total * 1000, false, false, TimerStatus.TimeUp)]
    [InlineData(Total * 1000, false, true, TimerStatus.Paused)] // At exactly zero with overtime: not over yet
    public void Status_FollowsTheState(long elapsedMilliseconds, bool running, bool allowOvertime, TimerStatus expected)
    {
        var readout = TimerReadout.From(new TimerState(Total, elapsedMilliseconds, running, allowOvertime));

        Assert.Equal(expected, readout.Status);
    }

    [Theory]
    [InlineData(0, "50:00")]
    [InlineData(1, "50:00")] // Rounds up while counting down
    [InlineData(1_000, "49:59")]
    [InlineData(Total * 1000 - 1, "00:01")]
    [InlineData(Total * 1000, "00:00")]
    [InlineData(Total * 1000 + 999, "+00:00")] // Rounds down past zero
    [InlineData(Total * 1000 + 65_000, "+01:05")]
    public void Time_CountsDownThenUp(long elapsedMilliseconds, string expected)
    {
        var readout = TimerReadout.From(new TimerState(Total, elapsedMilliseconds, Running: true, AllowOvertime: true));

        Assert.Equal(expected, readout.Time);
    }

    [Fact]
    public void Time_PastZeroWithoutOvertime_StaysAtZero()
    {
        // The server stops it at zero; this covers a snapshot taken a moment late.
        var readout = TimerReadout.From(new TimerState(Total, Total * 1000 + 300, Running: true, AllowOvertime: false));

        Assert.Equal("00:00", readout.Time);
        Assert.Equal(TimerStatus.TimeUp, readout.Status);
    }

    [Theory]
    [InlineData(0, TimerTone.Normal)]
    [InlineData((Total - TimerReadout.LowTimeSeconds) * 1000, TimerTone.Normal)]
    [InlineData((Total - TimerReadout.LowTimeSeconds) * 1000 + 1, TimerTone.LowTime)]
    [InlineData(Total * 1000 + 1, TimerTone.Overtime)]
    public void Tone_FollowsTheTimeLeft(long elapsedMilliseconds, TimerTone expected)
    {
        var readout = TimerReadout.From(new TimerState(Total, elapsedMilliseconds, Running: true, AllowOvertime: true));

        Assert.Equal(expected, readout.Tone);
    }

    [Fact]
    public void Tone_ShortTimerNotStarted_IsNormal()
    {
        var readout = TimerReadout.From(new TimerState(60, 0, Running: false, AllowOvertime: true));

        Assert.Equal(TimerTone.Normal, readout.Tone);
    }

    [Theory]
    [InlineData(Total, 0, 0.0)]
    [InlineData(Total, Total * 500, 0.5)]
    [InlineData(Total, Total * 2000, 1.0)] // Capped in overtime
    [InlineData(0, 5_000, 0.0)] // No total: nothing to measure against
    public void Progress_IsTheFractionRun(int totalSeconds, long elapsedMilliseconds, double expected)
    {
        var readout = TimerReadout.From(new TimerState(totalSeconds, elapsedMilliseconds, Running: true, AllowOvertime: true));

        Assert.Equal(expected, readout.Progress, precision: 6);
    }
}
