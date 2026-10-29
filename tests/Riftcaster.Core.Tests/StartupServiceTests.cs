using Microsoft.Extensions.Logging.Abstractions;
using Riftcaster.Contracts;
using Riftcaster.Core.Settings;
using Riftcaster.Core.Startup;

namespace Riftcaster.Core.Tests;

// What the Settings page's startup section knows: whether Riftcaster starts with Windows (kept by Windows, not
// by the settings), and the saved choice of opening the web interface.
public class StartupServiceTests
{
    // Stands in for the registry, which a test must not touch.
    private sealed class FakeEntry : IStartupEntry
    {
        public bool Exists { get; set; }

        public Exception? Refuses { get; set; }

        public bool Refuses_Silently { get; set; }

        public void Set(bool on)
        {
            if (Refuses is not null)
            {
                throw Refuses;
            }

            if (!Refuses_Silently)
            {
                Exists = on;
            }
        }
    }

    private readonly InMemoryAppSettingsStore _store = new();

    private readonly AppSettingsService _settings;

    private readonly StartupService _startup;

    private readonly FakeEntry _entry = new();

    public StartupServiceTests()
    {
        _settings = new AppSettingsService(_store);
        _startup = new StartupService(_settings, StartupAvailability.NotInstalled, NullLogger<StartupService>.Instance);
    }

    [Fact]
    public void BeforeAnEntryIsAttached_ItCannotStartWithWindows_AndSaysWhy()
    {
        Assert.Equal(StartupAvailability.NotInstalled, _startup.Availability);
        Assert.False(_startup.StartsWithWindows);
        Assert.False(_startup.SetStartsWithWindows(true));
    }

    [Fact]
    public void TheWindowsAppCanSayWhyItCannot()
    {
        var changes = 0;
        _startup.Changed += () => changes++;

        _startup.Unavailable(StartupAvailability.ConsoleServer);

        Assert.Equal(StartupAvailability.ConsoleServer, _startup.Availability);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void AnAttachedEntry_MakesItAvailable_AndIsReadEachTime()
    {
        var changes = 0;
        _startup.Changed += () => changes++;

        _startup.Attach(_entry);

        Assert.Equal(StartupAvailability.Available, _startup.Availability);
        Assert.Equal(1, changes);
        Assert.False(_startup.StartsWithWindows);

        _entry.Exists = true; // Added by hand, or removed from Task Manager's Startup tab
        Assert.True(_startup.StartsWithWindows);
    }

    [Fact]
    public void StartingWithWindows_IsAddedAndRemoved()
    {
        _startup.Attach(_entry);

        Assert.True(_startup.SetStartsWithWindows(true));
        Assert.True(_entry.Exists);

        Assert.True(_startup.SetStartsWithWindows(false));
        Assert.False(_entry.Exists);
    }

    [Fact]
    public void AChange_IsAnnounced()
    {
        _startup.Attach(_entry);
        var changes = 0;
        _startup.Changed += () => changes++;

        _startup.SetStartsWithWindows(true);

        Assert.Equal(1, changes);
    }

    // A policy can lock the Run list: the page says so rather than showing a switch that quietly went back.
    [Theory]
    [InlineData(typeof(UnauthorizedAccessException))]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(System.Security.SecurityException))]
    public void WhenWindowsRefuses_ItIsSaidSo_AndNothingIsThrown(Type exception)
    {
        _startup.Attach(_entry);
        _entry.Refuses = (Exception)Activator.CreateInstance(exception)!;

        Assert.False(_startup.SetStartsWithWindows(true));
        Assert.False(_startup.StartsWithWindows);
    }

    [Fact]
    public void AnEntryThatDidNotChange_IsNotReportedAsDone()
    {
        _startup.Attach(_entry);
        _entry.Refuses_Silently = true;

        Assert.False(_startup.SetStartsWithWindows(true));
    }

    [Fact]
    public void OpeningTheWebInterface_IsOnUntilChosen_AndFollowsTheSetting()
    {
        Assert.True(_startup.OpenWebUI);

        _settings.Update(settings => settings with { Startup = new StartupSettings { OpenWebUI = false } });

        Assert.False(_startup.OpenWebUI);
        Assert.False(_store.Settings!.Startup.OpenWebUI); // Saved
    }
}
