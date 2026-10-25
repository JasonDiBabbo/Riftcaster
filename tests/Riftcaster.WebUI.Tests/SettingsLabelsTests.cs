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
}
