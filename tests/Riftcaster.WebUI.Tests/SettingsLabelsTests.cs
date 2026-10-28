using Riftcaster.Contracts;
using Riftcaster.WebUI.Settings;

namespace Riftcaster.WebUI.Tests;

// The words the Settings page uses for its choices.
public class SettingsLabelsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, "just now")]
    [InlineData(59, "just now")]
    [InlineData(60, "1 minute ago")]
    [InlineData(5 * 60, "5 minutes ago")]
    [InlineData(60 * 60, "1 hour ago")]
    [InlineData(2 * 60 * 60 + 1800, "2 hours ago")]
    [InlineData(24 * 60 * 60, "1 day ago")]
    [InlineData(3 * 24 * 60 * 60, "3 days ago")]
    public void Ago_SaysHowLongInTheLargestWholeUnit(int seconds, string expected)
    {
        Assert.Equal(expected, SettingsLabels.Ago(Now.AddSeconds(-seconds), Now));
    }

    [Fact]
    public void EveryUpdateModeAndChannel_HasALabelAndADescription()
    {
        Assert.Equal(Enum.GetValues<UpdateMode>(), SettingsLabels.UpdateModeOptions.Select(option => option.Value));
        Assert.Equal(Enum.GetValues<UpdateChannel>(), SettingsLabels.UpdateChannelOptions.Select(option => option.Value));
        Assert.All(Enum.GetValues<UpdateMode>(), mode => Assert.False(string.IsNullOrWhiteSpace(SettingsLabels.Describe(mode))));
        Assert.All(Enum.GetValues<UpdateChannel>(), channel => Assert.False(string.IsNullOrWhiteSpace(SettingsLabels.Describe(channel))));
    }

    [Fact]
    public void EveryCardSource_HasALabelAndADescription()
    {
        Assert.Equal(Enum.GetValues<CardSourceKind>(), SettingsLabels.CardSourceOptions.Select(option => option.Value));
        Assert.All(Enum.GetValues<CardSourceKind>(), source => Assert.False(string.IsNullOrWhiteSpace(SettingsLabels.Describe(source))));
    }

    // A zone with names of its own, so what's said doesn't depend on the computer the tests run on.
    private static readonly TimeZoneInfo Pacific = TimeZoneInfo.CreateCustomTimeZone("pacific", TimeSpan.FromHours(-7), "Pacific", "Pacific Standard Time", "Pacific Daylight Time", null);
    private static readonly TimeZoneInfo Nameless = TimeZoneInfo.CreateCustomTimeZone("nameless", TimeSpan.FromMinutes(330), "", "", "", null);

    [Fact]
    public void ZoneLabel_SaysTheOffsetFromUtcAndTheName()
    {
        var moment = new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal("UTC-07:00, Pacific Standard Time", SettingsLabels.ZoneLabel(moment, Pacific));
        Assert.Equal("UTC+05:30", SettingsLabels.ZoneLabel(moment, Nameless)); // Without a name, the offset alone
        Assert.Equal("UTC+00:00", SettingsLabels.ZoneLabel(moment, TimeZoneInfo.CreateCustomTimeZone("u", TimeSpan.Zero, "", "")));
    }

    // In words, in the time zone of the computer running Riftcaster (a test says which), which is said.
    [Theory]
    [InlineData(2026, 10, 25, 22, 10, "Available again today after 3:10 PM UTC-07:00, Pacific Standard Time")]
    [InlineData(2026, 10, 26, 16, 42, "Available again tomorrow after 9:42 AM UTC-07:00, Pacific Standard Time")]
    [InlineData(2026, 10, 28, 16, 42, "Available again on Oct 28 after 9:42 AM UTC-07:00, Pacific Standard Time")]
    public void AvailableAgain_SaysTodayTomorrowOrTheDate_AndTheTimeZone(int year, int month, int day, int hour, int minute, string expected)
    {
        var now = new DateTimeOffset(2026, 10, 25, 17, 0, 0, TimeSpan.Zero); // 10:00 AM in Pacific
        var notBefore = new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero);

        Assert.Equal(expected, SettingsLabels.AvailableAgain(notBefore, now, Pacific));
    }

    [Fact]
    public void AvailableAgain_IsInTheZoneGivenAndTheDayIsThatZones()
    {
        var now = new DateTimeOffset(2026, 10, 25, 20, 0, 0, TimeSpan.Zero); // 1:00 PM there
        var notBefore = new DateTimeOffset(2026, 10, 26, 3, 30, 0, TimeSpan.Zero); // 8:30 PM there, the same day

        Assert.Equal("Available again today after 8:30 PM UTC-07:00, Pacific Standard Time", SettingsLabels.AvailableAgain(notBefore, now, Pacific));
    }

    [Fact]
    public void AvailableAgain_ForATimeAlreadyPast_SaysToday()
    {
        var now = new DateTimeOffset(2026, 10, 25, 17, 0, 0, TimeSpan.Zero);

        Assert.StartsWith("Available again today", SettingsLabels.AvailableAgain(now.AddHours(-1), now, Pacific));
    }

    [Fact]
    public void AvailableAgain_WithoutAZone_UsesTheLocalOne()
    {
        var now = DateTimeOffset.Now;

        Assert.Contains("UTC", SettingsLabels.AvailableAgain(now.AddMinutes(5), now));
    }

    [Theory]
    [InlineData(2026, 10, 25, 22, 10, "today after 3:10 PM UTC-07:00, Pacific Standard Time")]
    [InlineData(2026, 10, 26, 16, 42, "tomorrow after 9:42 AM UTC-07:00, Pacific Standard Time")]
    [InlineData(2026, 10, 29, 7, 5, "on Oct 29 after 12:05 AM UTC-07:00, Pacific Standard Time")]
    public void When_SaysTheTimeWithoutTheSentenceAroundIt(int year, int month, int day, int hour, int minute, string expected)
    {
        var now = new DateTimeOffset(2026, 10, 25, 17, 0, 0, TimeSpan.Zero);

        Assert.Equal(expected, SettingsLabels.When(new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero), now, Pacific));
    }

    [Fact]
    public void Timestamp_IsTheDateAndTimeInTheZoneWithTheZoneSaid()
    {
        var moment = new DateTimeOffset(2026, 10, 26, 16, 42, 0, TimeSpan.Zero);

        Assert.Equal("Oct 26, 2026 at 9:42 AM UTC-07:00, Pacific Standard Time", SettingsLabels.Timestamp(moment, Pacific));
        Assert.Equal("Oct 26, 2026 at 10:12 PM UTC+05:30", SettingsLabels.Timestamp(moment, Nameless));
        Assert.Matches(@"^[A-Z][a-z]{2} \d{1,2}, \d{4} at \d{1,2}:\d{2} (AM|PM) UTC[+-]\d{2}:\d{2}", SettingsLabels.Timestamp(moment));
    }

    [Fact]
    public void TheCardSourceDescriptions_AreTheOnesChosen()
    {
        Assert.Equal("Riftcodex is fetched page by page, which can take a few minutes.", SettingsLabels.Describe(CardSourceKind.Riftcodex));
        Assert.Equal("RiftHunt serves their whole catalog as one download. At their request, users will only be allowed to download the card catalog once per day to avoid stress on their service.", SettingsLabels.Describe(CardSourceKind.RiftHunt));
    }
}
