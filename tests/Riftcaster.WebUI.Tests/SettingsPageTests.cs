using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Riftcaster.Contracts;
using Riftcaster.Core.Settings;
using Riftcaster.WebUI.Settings;

namespace Riftcaster.WebUI.Tests;

// The Settings page's sections.
public class SettingsPageTests : BunitContext
{
    private readonly AppSettingsService _settings = new(new InMemoryAppSettingsStore());

    public SettingsPageTests()
    {
        Services.AddSingleton(_settings);
    }

    [Fact]
    public void Logging_ChoosesTheLogDetail()
    {
        var section = Render<LoggingSection>();

        section.FindAll("[aria-label='Log detail'] button").Single(button => button.TextContent.Trim() == "Detailed").Click();

        Assert.Equal(LogDetail.Detailed, _settings.Settings.Logging.Detail);
    }

    [Fact]
    public void Logging_ShowsTheSavedDetailAndWhatItLogs()
    {
        _settings.Update(settings => settings with { Logging = new LoggingSettings { Detail = LogDetail.WarningsOnly } });

        var section = Render<LoggingSection>();

        var selected = section.FindAll("[aria-label='Log detail'] button[aria-pressed='true']").Single();
        Assert.Equal("Warnings only", selected.TextContent.Trim());
        Assert.Contains(SettingsLabels.Describe(LogDetail.WarningsOnly), section.Markup);
    }

    [Fact]
    public void Logging_FollowsChangesMadeElsewhere()
    {
        var section = Render<LoggingSection>();

        _settings.Update(settings => settings with { Logging = new LoggingSettings { Detail = LogDetail.Detailed } });

        section.WaitForAssertion(() =>
            Assert.Equal("Detailed", section.Find("[aria-label='Log detail'] button[aria-pressed='true']").TextContent.Trim()));
    }

    // On a phone the note sits in the description; beside a wider control it's under the control.
    // Which shows is the CSS's job: both are in the markup.
    [Fact]
    public void Logging_NoteIsInTheDescriptionAndUnderTheControl()
    {
        var section = Render<LoggingSection>();

        Assert.Equal("Applies immediately.", section.Find(".description .note-inline").TextContent.Trim());
        Assert.Equal("Applies immediately", section.Find(".control .note").TextContent.Trim());
    }

    [Fact]
    public void Logging_StopsListeningWhenRemoved()
    {
        var section = Render<LoggingSection>();

        section.Dispose();

        // Would throw if the removed section still tried to render.
        _settings.Update(settings => settings with { Logging = new LoggingSettings { Detail = LogDetail.Detailed } });
    }

    [Theory]
    [InlineData(LogDetail.WarningsOnly)]
    [InlineData(LogDetail.Normal)]
    [InlineData(LogDetail.Detailed)]
    public void EveryLogDetail_HasALabelAndADescription(LogDetail detail)
    {
        Assert.Single(SettingsLabels.LogDetailOptions, option => option.Value == detail);
        Assert.False(string.IsNullOrWhiteSpace(SettingsLabels.Describe(detail)));
    }
}
