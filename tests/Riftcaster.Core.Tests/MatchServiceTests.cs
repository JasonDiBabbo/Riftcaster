using Riftcaster.Contracts;
using Riftcaster.Core.Match;

namespace Riftcaster.Core.Tests;

public class MatchServiceTests
{
    private readonly InMemoryMatchStore _store = new();

    private readonly MatchService _service;

    private int _changedCount;

    public MatchServiceTests()
    {
        _service = new MatchService(_store);
        _service.Changed += () => _changedCount++;
    }

    [Fact]
    public void Constructor_LoadsSavedSettings()
    {
        var saved = MatchRules.Default with { Mode = MatchMode.FreeForAll3, DurationMinutes = 30 };
        _store.Save(saved);

        var service = new MatchService(_store);

        Assert.Equal(saved, service.Settings);
    }

    [Fact]
    public void Constructor_NormalizesSavedSettings()
    {
        _store.Save(MatchRules.Default with { PointsToWin = 0 });

        var service = new MatchService(_store);

        Assert.Equal(MatchService.MinPointsToWin, service.Settings.PointsToWin);
    }

    [Fact]
    public void DefaultSettings_AreInitiallyApplied()
    {
        Assert.Equal(MatchRules.Default, _service.Settings);
    }

    [Fact]
    public void Update_AppliesChange()
    {
        var newPointsToWin = 15;
        _service.Update(settings => settings with { PointsToWin = newPointsToWin });

        Assert.Equal(MatchRules.Default with { PointsToWin = newPointsToWin }, _service.Settings);
    }

    [Fact]
    public void Update_RaisesChangedOnce()
    {
        _service.Update(settings => settings with { PointsToWin = 15 });

        Assert.Equal(1, _changedCount);
    }

    [Fact]
    public void Update_SameContent_DoesNotRaiseChanged()
    {
        // already Best of 3: a new object with the same values
        _service.Update(settings => settings with { Format = MatchFormat.BestOf3 });

        Assert.Equal(0, _changedCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Update_PointsToWinBelowOne_ClampsToMinimum(int pointsToWin)
    {
        _service.Update(settings => settings with { PointsToWin = pointsToWin });

        Assert.Equal(MatchService.MinPointsToWin, _service.Settings.PointsToWin);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Update_DurationMinutesBelowOne_ClampsToMinimum(int durationMinutes)
    {
        _service.Update(settings => settings with { DurationMinutes = durationMinutes });

        Assert.Equal(MatchService.MinDurationMinutes, _service.Settings.DurationMinutes);
    }

    [Fact]
    public void Update_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _service.Update(null!);
        });
    }

    [Fact]
    public void Update_SavesTheNewSettings()
    {
        _service.Update(settings => settings with { AllowOvertime = false });

        Assert.Same(_service.Settings, _store.Settings);
    }

    [Fact]
    public void Update_NoChange_DoesNotSave()
    {
        _service.Update(settings => settings with { Format = MatchFormat.BestOf3 });

        Assert.Equal(0, _store.SaveCount);
    }
}
