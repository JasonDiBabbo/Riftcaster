using Riftcaster.Contracts;
using Riftcaster.Core.Match;
using Riftcaster.Core.Settings;

namespace Riftcaster.Core.Tests;

// The match defaults: the rules that resetting the current match gives it, chosen on the Settings page.
public class MatchDefaultsTests
{
    private readonly InMemoryAppSettingsStore _store = new();

    private readonly AppSettingsService _settings;

    private readonly MatchService _match = new(new InMemoryMatchStore());

    public MatchDefaultsTests()
    {
        _settings = new AppSettingsService(_store);
    }

    private static AppSettings WithDefaults(MatchSettings rules) => new() { MatchDefaults = rules };

    [Fact]
    public void MatchDefaults_AreTheDesignsUntilChosen()
    {
        Assert.Null(_settings.Settings.MatchDefaults);
        Assert.Equal(MatchRules.Default, _settings.MatchDefaults);
    }

    [Fact]
    public void MatchDefaults_AreTheChosenOnes()
    {
        var chosen = MatchRules.Default with { Mode = MatchMode.FreeForAll4, DurationMinutes = 30 };

        _settings.Update(settings => settings with { MatchDefaults = chosen });

        Assert.Equal(chosen, _settings.MatchDefaults);
        Assert.Equal(chosen, _store.Settings!.MatchDefaults); // Saved
    }

    [Fact]
    public void MatchDefaults_ComeBackFromTheStore()
    {
        var chosen = MatchRules.Default with { Format = MatchFormat.BestOf1 };
        _store.Save(WithDefaults(chosen));

        var service = new AppSettingsService(_store);

        Assert.Equal(chosen, service.MatchDefaults);
    }

    [Fact]
    public void MatchDefaults_AreKeptWithinTheMatchLimits()
    {
        _settings.Update(settings => settings with { MatchDefaults = MatchRules.Default with { PointsToWin = 0, DurationMinutes = 999 } });

        Assert.Equal(MatchService.MinPointsToWin, _settings.MatchDefaults.PointsToWin);
        Assert.Equal(MatchService.MaxDurationMinutes, _settings.MatchDefaults.DurationMinutes);
    }

    // The JSON also accepts numbers for the enums. Applying a mode nobody knows would break the match.
    [Fact]
    public void MatchDefaults_PutAnUnknownModeOrFormatBackToTheDesigns()
    {
        _store.Save(WithDefaults(MatchRules.Default with { Mode = (MatchMode)99, Format = (MatchFormat)99 }));

        var service = new AppSettingsService(_store);

        Assert.Equal(MatchRules.Default.Mode, service.MatchDefaults.Mode);
        Assert.Equal(MatchRules.Default.Format, service.MatchDefaults.Format);
    }

    [Fact]
    public void ResetToDefaults_GivesTheMatchTheChosenRules()
    {
        var chosen = MatchRules.Default with { Mode = MatchMode.TwoVsTwo, PointsToWin = 12, AllowOvertime = false };
        _settings.Update(settings => settings with { MatchDefaults = chosen });
        _match.Update(match => match with { Mode = MatchMode.FreeForAll3, PointsToWin = 9, DurationMinutes = 20 });

        _match.ResetToDefaults(_settings);

        Assert.Equal(chosen, _match.Settings);
    }

    [Fact]
    public void ResetToDefaults_WithNoneChosen_GivesTheDesignsRules()
    {
        _match.Update(match => match with { Mode = MatchMode.FreeForAll3, DurationMinutes = 20 });

        _match.ResetToDefaults(_settings);

        Assert.Equal(MatchRules.Default, _match.Settings);
    }

    [Fact]
    public void ResetToDefaults_RaisesTheMatchChanged()
    {
        _match.Update(match => match with { Mode = MatchMode.FreeForAll3 });
        var raised = 0;
        _match.Changed += () => raised++;

        _match.ResetToDefaults(_settings);
        _match.ResetToDefaults(_settings); // Already there: nothing changes

        Assert.Equal(1, raised);
    }
}
