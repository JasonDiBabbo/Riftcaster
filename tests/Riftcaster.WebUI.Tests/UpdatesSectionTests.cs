using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Riftcaster.Contracts;
using Riftcaster.Core.Settings;
using Riftcaster.Core.Updates;
using Riftcaster.WebUI.Layout;
using Riftcaster.WebUI.Settings;

namespace Riftcaster.WebUI.Tests;

// The Settings page's updates section: the mode and channel, what has been found, and what can be done about it.
public class UpdatesSectionTests : BunitContext
{
    private sealed class FakeFetcher : IUpdateFetcher
    {
        public string? DownloadedVersion { get; set; }

        public string? Finds { get; set; }

        public Exception? Fails { get; set; }

        public int Checks { get; private set; }

        public Task<string?> CheckAndDownloadAsync(UpdateChannel channel, CancellationToken cancellationToken)
        {
            Checks++;
            return Fails is not null ? throw Fails : Task.FromResult(Finds);
        }
    }

    private readonly AppSettingsService _settings = new(new InMemoryAppSettingsStore());

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero));

    private readonly FakeFetcher _fetcher = new();

    private UpdateService _updates = null!;

    private int _restarts;

    private IRenderedComponent<UpdatesSection> RenderSection(
        UpdateAvailability availability = UpdateAvailability.Updatable,
        bool local = true,
        string version = "0.2.0")
    {
        _updates = new UpdateService(_settings, version, UpdateAvailability.FromSource, _time, NullLogger<UpdateService>.Instance);
        if (availability == UpdateAvailability.Updatable)
        {
            _updates.Attach(_fetcher, () => _restarts++);
        }
        else
        {
            _updates.Unavailable(availability);
        }

        Services.AddSingleton(_settings);
        Services.AddSingleton(_updates);
        Services.AddSingleton<TimeProvider>(_time);
        return Render<UpdatesSection>(parameters => parameters.AddCascadingValue(new OperatorSession(IsLocal: local, CodeVersion: null)));
    }

    private void Choose(UpdateMode mode) => _settings.Update(settings => settings with { Updates = settings.Updates with { Mode = mode } });

    private static string Row(IRenderedComponent<UpdatesSection> section, string title) =>
        section.FindAll(".row").Single(row => row.QuerySelector(".title")!.TextContent.Trim() == title).QuerySelector(".description")!.TextContent;

    private static AngleSharp.Dom.IElement Segment(IRenderedComponent<UpdatesSection> section, string group, string label) =>
        section.Find($".segmented[aria-label='{group}']").QuerySelectorAll("button").Single(button => button.TextContent.Trim() == label);

    [Fact]
    public void AnUpdatableCopy_ShowsTheModeTheChannelAndTheVersion()
    {
        var section = RenderSection();

        Assert.Equal(["Updates", "Update channel", "Riftcaster 0.2.0"], section.FindAll(".row .title").Select(title => title.TextContent.Trim()));
        Assert.Equal(["Automatic", "Ask first", "Off"], section.Find(".segmented[aria-label='Updates']").QuerySelectorAll("button").Select(b => b.TextContent.Trim()));
        Assert.Equal(["Stable", "Pre-release"], section.Find(".segmented[aria-label='Update channel']").QuerySelectorAll("button").Select(b => b.TextContent.Trim()));
        Assert.Equal("true", Segment(section, "Updates", "Automatic").GetAttribute("aria-pressed"));
        Assert.Equal("true", Segment(section, "Update channel", "Stable").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void ChoosingAMode_SavesItAndSaysWhatItDoes()
    {
        var section = RenderSection();

        Segment(section, "Updates", "Ask first").Click();

        Assert.Equal(UpdateMode.AskFirst, _settings.Settings.Updates.Mode);
        Assert.Contains("only when you choose Restart to update", Row(section, "Updates"));
        Assert.Equal("true", Segment(section, "Updates", "Ask first").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void ChoosingAChannel_SavesIt()
    {
        var section = RenderSection();

        Segment(section, "Update channel", "Pre-release").Click();

        Assert.Equal(UpdateChannel.PreRelease, _settings.Settings.Updates.Channel);
        Assert.Contains("beta builds", Row(section, "Update channel"));
    }

    [Fact]
    public void ThePreReleaseBuild_ShowsThePreReleaseChannelUntilOneIsChosen()
    {
        var section = RenderSection(version: "0.3.0-beta.1");

        Assert.Equal("true", Segment(section, "Update channel", "Pre-release").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void WhenAnythingMayApply_TheRowsSayWhen()
    {
        var section = RenderSection();

        var notes = section.FindAll(".control .note").Select(note => note.TextContent.Trim());

        Assert.Equal(["Applies immediately", "Applies to the next check"], notes);
    }

    [Fact]
    public void BeforeAnyCheck_ItSaysWhenItLooks()
    {
        var section = RenderSection();

        Assert.Contains("soon after starting", Row(section, "Riftcaster 0.2.0"));
    }

    [Fact]
    public void CheckNow_ChecksAndSaysHowLongAgo()
    {
        var section = RenderSection();

        section.Find("button.check").Click();
        _time.Advance(TimeSpan.FromHours(2));
        section.Render();

        Assert.Equal(1, _fetcher.Checks);
        Assert.Contains("You're up to date. Last checked 2 hours ago.", Row(section, "Riftcaster 0.2.0"));
    }

    [Fact]
    public void AFailedCheck_SaysSo()
    {
        _fetcher.Fails = new HttpRequestException("No such host");
        var section = RenderSection();

        section.Find("button.check").Click();

        Assert.Contains("Couldn't check for updates", Row(section, "Riftcaster 0.2.0"));
        Assert.Contains("Check the log", Row(section, "Riftcaster 0.2.0"));
    }

    [Fact]
    public void WhenOff_NothingCanBeChecked_AndItSays()
    {
        Choose(UpdateMode.Off);

        var section = RenderSection();

        Assert.True(section.Find("button.check").HasAttribute("disabled"));
        Assert.Contains("Updates are off. Staying on this version.", Row(section, "Riftcaster 0.2.0"));
    }

    [Fact]
    public void AReadyUpdate_InAutomatic_InstallsWhenItQuits_OrNowOnRequest()
    {
        _fetcher.DownloadedVersion = "0.3.0";

        var section = RenderSection();

        Assert.Contains("Version 0.3.0 is downloaded and ready.", Row(section, "Riftcaster 0.2.0"));
        Assert.Equal("Ready to install", section.Find(".box b").TextContent);
        Assert.Contains("It installs when Riftcaster quits", section.Find(".box").TextContent);
        Assert.True(section.Find("button.check").HasAttribute("disabled")); // Nothing newer is fetched until this is installed

        section.Find("button.restart").Click();

        Assert.Equal(1, _restarts);
    }

    [Fact]
    public void AReadyUpdate_InAskFirst_WaitsForTheRestart()
    {
        _fetcher.DownloadedVersion = "0.3.0";
        Choose(UpdateMode.AskFirst);

        var section = RenderSection();

        Assert.Equal("Version 0.3.0 is ready", section.Find(".box b").TextContent);
        Assert.Contains("Quitting Riftcaster leaves 0.2.0 in place", section.Find(".box").TextContent);
        Assert.NotNull(section.Find("button.restart"));
    }

    [Fact]
    public void AReadyUpdate_WhenOff_WontBeInstalled_AndCantBeRestartedInto()
    {
        _fetcher.DownloadedVersion = "0.3.0";
        Choose(UpdateMode.Off);

        var section = RenderSection();

        Assert.Equal("Version 0.3.0 won't be installed", section.Find(".box b").TextContent);
        Assert.Contains("Switch to Ask first to install it, or leave Updates off to stay on 0.2.0.", section.Find(".box").TextContent);
        Assert.Empty(section.FindAll("button.restart"));
        Assert.DoesNotContain("ready", section.Find(".box").ClassName);
    }

    [Fact]
    public void TurningUpdatesOff_ChangesWhatAReadyUpdateSays()
    {
        _fetcher.DownloadedVersion = "0.3.0";
        var section = RenderSection();
        Assert.NotEmpty(section.FindAll("button.restart"));

        Choose(UpdateMode.Off);

        section.WaitForAssertion(() => Assert.Empty(section.FindAll("button.restart")));
        Assert.Equal("Version 0.3.0 won't be installed", section.Find(".box b").TextContent);
    }

    // The computer running Riftcaster is the one to restart it: a device that did could lose its own connection.
    [Fact]
    public void FromAnotherDevice_ThereIsNoRestart_AndNothingHappensIfAskedFor()
    {
        _fetcher.DownloadedVersion = "0.3.0";

        var section = RenderSection(local: false);

        Assert.Empty(section.FindAll("button.restart"));
        Assert.NotEmpty(section.FindAll(".box"));
        Assert.NotEmpty(section.FindAll("button.check")); // Anyone can look for an update
        Assert.Equal(0, _restarts);
    }

    [Fact]
    public void RunningFromSource_SaysSoInPlaceOfTheSettings()
    {
        var section = RenderSection(UpdateAvailability.FromSource);

        Assert.Equal("Riftcaster 0.2.0 (development build)", section.Find(".row .title").TextContent.Trim());
        Assert.Equal("This copy is running from source. Update it with git.", section.Find(".row .description").TextContent.Trim());
        Assert.Empty(section.FindAll(".segmented"));
        Assert.Empty(section.FindAll("button"));
    }

    [Fact]
    public void TheConsoleServerOfAnInstalledFolder_SaysTheTrayAppDoesIt()
    {
        var section = RenderSection(UpdateAvailability.ConsoleInInstalledFolder);

        Assert.Equal("Riftcaster 0.2.0", section.Find(".row .title").TextContent.Trim());
        Assert.Equal("Updates are handled by the tray app. Start Riftcaster.exe to get them.", section.Find(".row .description").TextContent.Trim());
        Assert.Empty(section.FindAll(".segmented"));
    }

    [Fact]
    public void AnInstalledCopyWithUpdatesTurnedOffInItsConfiguration_SaysSo()
    {
        var section = RenderSection(UpdateAvailability.TurnedOffInConfiguration);

        Assert.Contains("Updates:Source is empty", section.Find(".row .description").TextContent);
        Assert.Empty(section.FindAll(".segmented"));
    }

    [Fact]
    public async Task Dispose_StopsListening()
    {
        var section = RenderSection();

        await DisposeComponentsAsync();

        // Would throw if the removed section still tried to render.
        Choose(UpdateMode.Off);
        Assert.NotNull(section);
    }
}
