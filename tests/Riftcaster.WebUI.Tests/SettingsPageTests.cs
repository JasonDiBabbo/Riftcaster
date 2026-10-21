using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Riftcaster.Contracts;
using Riftcaster.Core.Diagnostics;
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
