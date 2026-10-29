using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Riftcaster.Core.Settings;
using Riftcaster.Core.Startup;
using Riftcaster.WebUI.Layout;
using Riftcaster.WebUI.Settings;

namespace Riftcaster.WebUI.Tests;

// The Settings page's startup section: starting with Windows, and opening the web interface when Riftcaster starts.
public class StartupSectionTests : BunitContext
{
    private sealed class FakeEntry : IStartupEntry
    {
        public bool Exists { get; set; }

        public bool Refuses { get; set; }

        public void Set(bool on)
        {
            if (Refuses)
            {
                throw new UnauthorizedAccessException();
            }

            Exists = on;
        }
    }

    private readonly AppSettingsService _settings = new(new InMemoryAppSettingsStore());

    private readonly FakeEntry _entry = new();

    private StartupService _startup = null!;

    private IRenderedComponent<StartupSection> RenderSection(StartupAvailability availability = StartupAvailability.Available, bool local = true)
    {
        _startup = new StartupService(_settings, StartupAvailability.ConsoleServer, NullLogger<StartupService>.Instance);
        if (availability == StartupAvailability.Available)
        {
            _startup.Attach(_entry);
        }
        else
        {
            _startup.Unavailable(availability);
        }

        Services.AddSingleton(_settings);
        Services.AddSingleton(_startup);
        return Render<StartupSection>(parameters => parameters.AddCascadingValue(new OperatorSession(IsLocal: local, CodeVersion: null)));
    }

    private static AngleSharp.Dom.IElement Switch(IRenderedComponent<StartupSection> section, string label) =>
        section.Find($"button[role='switch'][aria-label='{label}']");

    [Fact]
    public void TheConsoleServer_HasNoStartupSection()
    {
        var section = RenderSection(StartupAvailability.ConsoleServer);

        Assert.Empty(section.Markup.Trim());
    }

    [Fact]
    public void StartWithWindows_ShowsTheEntry_AndAddsAndRemovesIt()
    {
        var section = RenderSection();
        Assert.Equal("false", Switch(section, "Start with Windows").GetAttribute("aria-checked"));

        Switch(section, "Start with Windows").Click();

        Assert.True(_entry.Exists);
        Assert.Equal("true", Switch(section, "Start with Windows").GetAttribute("aria-checked"));

        Switch(section, "Start with Windows").Click();

        Assert.False(_entry.Exists);
        Assert.Empty(section.FindAll(".problem"));
    }

    [Fact]
    public void WhenWindowsRefuses_TheSectionSaysSo()
    {
        var section = RenderSection();
        _entry.Refuses = true;

        Switch(section, "Start with Windows").Click();

        Assert.Equal("false", Switch(section, "Start with Windows").GetAttribute("aria-checked"));
        Assert.Contains("wouldn't let Riftcaster add itself to", section.Find(".problem").TextContent);
    }

    [Fact]
    public void WhenWindowsRefusesToRemoveIt_TheSectionSaysSo()
    {
        _entry.Exists = true;
        var section = RenderSection();
        _entry.Refuses = true;

        Switch(section, "Start with Windows").Click();

        Assert.Contains("remove itself from", section.Find(".problem").TextContent);
    }

    [Fact]
    public void AnEntryRemovedInWindows_ShowsOffTheNextTimeItDraws()
    {
        _entry.Exists = true;
        var section = RenderSection();
        Assert.Equal("true", Switch(section, "Start with Windows").GetAttribute("aria-checked"));

        _entry.Exists = false;
        _settings.Update(settings => settings with { Startup = new Riftcaster.Contracts.StartupSettings { OpenWebUI = false } });

        Assert.Equal("false", Switch(section, "Start with Windows").GetAttribute("aria-checked"));
    }

    [Fact]
    public void AnUninstalledCopy_SaysItNeedsSetup_AndCannotStartWithWindows()
    {
        var section = RenderSection(StartupAvailability.NotInstalled);

        Assert.True(Switch(section, "Start with Windows").HasAttribute("disabled"));
        Assert.Contains("Available once Riftcaster is installed", section.Markup);
        Assert.False(Switch(section, "Open the web interface on launch").HasAttribute("disabled")); // Still the launcher's, installed or not
    }

    [Fact]
    public void OpenTheWebInterface_IsOnUntilChosen_AndIsSaved()
    {
        var section = RenderSection();
        Assert.Equal("true", Switch(section, "Open the web interface on launch").GetAttribute("aria-checked"));

        Switch(section, "Open the web interface on launch").Click();

        Assert.False(_settings.Settings.Startup.OpenWebUI);
        Assert.Equal("false", Switch(section, "Open the web interface on launch").GetAttribute("aria-checked"));
    }

    [Fact]
    public void AnotherDevice_CanSeeTheSection_ButNotChangeIt()
    {
        var section = RenderSection(local: false);

        Assert.True(Switch(section, "Start with Windows").HasAttribute("disabled"));
        Assert.True(Switch(section, "Open the web interface on launch").HasAttribute("disabled"));
        Assert.Contains("Only the computer running Riftcaster can change startup settings.", section.Find(".explanation").TextContent);
    }

    // The switch is disabled for another device, but a handler that got through would still be the server's to refuse.
    [Fact]
    public void AnotherDevice_CannotAddTheEntry_EvenIfTheClickGetsThrough()
    {
        var section = RenderSection(local: false);

        section.Find("button[role='switch'][aria-label='Start with Windows']").RemoveAttribute("disabled");
        Switch(section, "Start with Windows").Click();

        Assert.False(_entry.Exists);
    }
}
