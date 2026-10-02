using Riftcaster.Core.Timer;

namespace Riftcaster.Core.Tests;

public class TimerFormatTests
{
    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(5, "00:05")]
    [InlineData(65, "01:05")]
    [InlineData(3000, "50:00")]
    [InlineData(10800, "180:00")]
    public void Format_WritesMinutesAndSeconds(int seconds, string expected)
    {
        var text = TimerFormat.Format(seconds);

        Assert.Equal(expected, text);
    }

    [Fact]
    public void Format_Negative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TimerFormat.Format(-1));
    }

    [Theory]
    [InlineData("12:30", 750)]
    [InlineData("0:05", 5)]
    [InlineData("00:00", 0)]
    [InlineData("90:00", 5400)]
    [InlineData("999:59", 59999)]
    [InlineData("45", 2700)]
    [InlineData("0", 0)]
    [InlineData("  7:15  ", 435)]
    public void TryParse_ValidTime_ReturnsSeconds(string text, int expected)
    {
        var parsed = TimerFormat.TryParse(text, out var seconds);

        Assert.True(parsed);
        Assert.Equal(expected, seconds);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("12:60")]
    [InlineData("-5")]
    [InlineData("+5")]
    [InlineData("1.5")]
    [InlineData("12:")]
    [InlineData(":30")]
    [InlineData("1:2:3")]
    [InlineData("12 :30")]
    [InlineData("1000")]
    [InlineData("1000:00")]
    [InlineData("99999999999")]
    public void TryParse_InvalidTime_ReturnsFalse(string? text)
    {
        var parsed = TimerFormat.TryParse(text, out var seconds);

        Assert.False(parsed);
        Assert.Equal(0, seconds);
    }
}
