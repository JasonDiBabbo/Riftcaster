using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Riftcaster.Contracts;
using Riftcaster.Core.Diagnostics;
using Riftcaster.Core.Match;
using Riftcaster.Core.Settings;
using Riftcaster.WebUI.Layout;
using Riftcaster.WebUI.Settings;

namespace Riftcaster.WebUI.Tests;

// The Settings page's sections.
public class SettingsPageTests : BunitContext
{
    private static readonly string LogFolderPath = Path.GetTempPath(); // Exists, so opening it needn't create it

    private readonly AppSettingsService _settings = new(new InMemoryAppSettingsStore());

    private readonly List<string> _opened = [];

    private bool _desktop = true;

    private Exception? _openFails;

    public SettingsPageTests()
    {
        Services.AddSingleton(_settings);
        Services.AddSingleton(new LogFolderOpener(
            new LogFolder(LogFolderPath),
            () => _desktop,
            path =>
            {
                if (_openFails is not null)
                {
                    throw _openFails;
                }

                _opened.Add(path);
            },
            NullLogger<LogFolderOpener>.Instance));
    }

    private IRenderedComponent<LoggingSection> RenderLogging(bool local = true) =>
        Render<LoggingSection>(parameters => parameters.AddCascadingValue(new OperatorSession(IsLocal: local, CodeVersion: null)));

    private static string Text(IRenderedComponent<LoggingSection> section, string selector) => section.Find(selector).TextContent.Trim();

    [Fact]
    public void Logging_ChoosesTheLogDetail()
    {
        var section = RenderLogging();

        section.FindAll("[aria-label='Log detail'] button").Single(button => button.TextContent.Trim() == "Detailed").Click();

        Assert.Equal(LogDetail.Detailed, _settings.Settings.Logging.Detail);
    }

    [Fact]
    public void Logging_ShowsTheSavedDetailAndWhatItLogs()
    {
        _settings.Update(settings => settings with { Logging = new LoggingSettings { Detail = LogDetail.WarningsOnly } });

        var section = RenderLogging();

        Assert.Equal("Warnings only", Text(section, "[aria-label='Log detail'] button[aria-pressed='true']"));
        Assert.Contains(SettingsLabels.Describe(LogDetail.WarningsOnly), section.Markup);
    }

    [Fact]
    public void Logging_FollowsChangesMadeElsewhere()
    {
        var section = RenderLogging();

        _settings.Update(settings => settings with { Logging = new LoggingSettings { Detail = LogDetail.Detailed } });

        section.WaitForAssertion(() => Assert.Equal("Detailed", Text(section, "[aria-label='Log detail'] button[aria-pressed='true']")));
    }

    // On a phone the note sits in the description; beside a wider control it's under the control.
    // Which shows is the CSS's job: both are in the markup.
    [Fact]
    public void Logging_NoteIsInTheDescriptionAndUnderTheControl()
    {
        var section = RenderLogging();

        Assert.Equal("Applies immediately.", Text(section, ".description .note-inline"));
        Assert.Equal("Applies immediately", Text(section, ".control .note"));
    }

    [Fact]
    public void Logging_StopsListeningWhenRemoved()
    {
        var section = RenderLogging();

        section.Dispose();

        // Would throw if the removed section still tried to render.
        _settings.Update(settings => settings with { Logging = new LoggingSettings { Detail = LogDetail.Detailed } });
    }

    private IRenderedComponent<MatchDefaultsSection> RenderDefaults() => Render<MatchDefaultsSection>();

    [Fact]
    public void MatchDefaults_ShowTheDesignsUntilChosen()
    {
        var section = RenderDefaults();

        Assert.Equal("1v1", section.Find("[aria-label='Default mode'] button[aria-pressed='true']").TextContent.Trim());
        Assert.Equal("Best of 3", section.Find("[aria-label='Default format'] button[aria-pressed='true']").TextContent.Trim());
        Assert.Equal("8", section.Find("input[aria-label='Default points to win']").GetAttribute("value"));
        Assert.Equal("50", section.Find("input[aria-label='Default duration in minutes']").GetAttribute("value"));
        Assert.Equal("true", section.Find("button[aria-label='Allow overtime by default']").GetAttribute("aria-checked"));
        Assert.Null(_settings.Settings.MatchDefaults); // Looking changes nothing
    }

    [Fact]
    public void MatchDefaults_ChooseTheModeAndFormat()
    {
        var section = RenderDefaults();

        section.FindAll("[aria-label='Default mode'] button").Single(button => button.TextContent.Trim() == "2v2").Click();
        section.FindAll("[aria-label='Default format'] button").Single(button => button.TextContent.Trim() == "Best of 1").Click();

        Assert.Equal(MatchMode.TwoVsTwo, _settings.MatchDefaults.Mode);
        Assert.Equal(MatchFormat.BestOf1, _settings.MatchDefaults.Format);
    }

    [Fact]
    public void MatchDefaults_ChangeThePointsDurationAndOvertime()
    {
        var section = RenderDefaults();

        section.Find("button[aria-label='Increase Default points to win']").Click();
        section.Find("button[aria-label='Increase Default duration in minutes']").Click();
        section.Find("button[aria-label='Allow overtime by default']").Click();

        Assert.Equal(MatchService.MinPointsToWin + 1, _settings.MatchDefaults.PointsToWin);
        Assert.Equal(55, _settings.MatchDefaults.DurationMinutes); // In steps of 5
        Assert.False(_settings.MatchDefaults.AllowOvertime);
    }

    [Fact]
    public void MatchDefaults_ChangingOneKeepsTheOthers()
    {
        var section = RenderDefaults();
        section.FindAll("[aria-label='Default mode'] button").Single(button => button.TextContent.Trim() == "FFA 4").Click();

        section.Find("button[aria-label='Increase Default points to win']").Click();

        Assert.Equal(MatchMode.FreeForAll4, _settings.MatchDefaults.Mode);
        Assert.Equal(MatchService.MinPointsToWin + 1, _settings.MatchDefaults.PointsToWin);
    }

    [Fact]
    public void MatchDefaults_FollowChangesMadeElsewhere()
    {
        var section = RenderDefaults();

        _settings.Update(settings => settings with { MatchDefaults = MatchRules.Default with { Mode = MatchMode.FreeForAll3 } });

        section.WaitForAssertion(() =>
            Assert.Equal("FFA 3", section.Find("[aria-label='Default mode'] button[aria-pressed='true']").TextContent.Trim()));
    }

    [Fact]
    public void MatchDefaults_StopListeningWhenRemoved()
    {
        var section = RenderDefaults();

        section.Dispose();

        // Would throw if the removed section still tried to render.
        _settings.Update(settings => settings with { MatchDefaults = MatchRules.Default with { Mode = MatchMode.FreeForAll3 } });
    }

    [Fact]
    public void LogFolder_ShowsThePathAndHowManyFilesAreKept()
    {
        var section = RenderLogging();

        Assert.Equal(LogFolderPath, Text(section, ".folder-path"));
        Assert.Equal($"Only one log file is written per day. The {LogFolder.RetainedFiles} most recent log files are kept.", Text(section, ".folder-note"));
    }

    [Fact]
    public void LogFolder_OpenFolderOpensIt()
    {
        var section = RenderLogging();

        section.Find(".folder-line button").Click();

        Assert.Equal([LogFolderPath], _opened);
        Assert.DoesNotContain("Couldn't", Text(section, ".folder-note"));
    }

    [Fact]
    public void LogFolder_WhenItCantBeOpened_SaysSo()
    {
        _openFails = new IOException();
        var section = RenderLogging();

        section.Find(".folder-line button").Click();

        Assert.Equal("Couldn't open the folder. The path above is where it is.", Text(section, ".folder-note"));
    }

    [Fact]
    public void LogFolder_OpeningAgainAfterAFailureClearsTheMessage()
    {
        _openFails = new IOException();
        var section = RenderLogging();
        section.Find(".folder-line button").Click();

        _openFails = null;
        section.Find(".folder-line button").Click();

        Assert.DoesNotContain("Couldn't", Text(section, ".folder-note"));
    }

    [Fact]
    public void LogFolder_FromAnotherDevice_CantBeOpened()
    {
        var section = RenderLogging(local: false);

        Assert.True(section.Find(".folder-line button").HasAttribute("disabled"));
        Assert.Contains("connected from another device", Text(section, ".folder-note"));
        Assert.Equal(LogFolderPath, Text(section, ".folder-path")); // The path is still there to read
        Assert.Empty(_opened);
    }

    [Fact]
    public void LogFolder_WithoutADesktop_HasNoButton()
    {
        _desktop = false;

        var section = RenderLogging();

        Assert.Empty(section.FindAll(".folder-line button"));
        Assert.Contains("no desktop session", Text(section, ".folder-note"));
        Assert.Equal(LogFolderPath, Text(section, ".folder-path"));
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
