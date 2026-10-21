using Riftcaster.Contracts;
using Riftcaster.Core.Settings;

namespace Riftcaster.Core.Tests;

public class AppSettingsServiceTests
{
    private readonly InMemoryAppSettingsStore _store = new();

    private readonly AppSettingsService _service;

    private int _changedCount;

    public AppSettingsServiceTests()
    {
        _service = new AppSettingsService(_store);
        _service.Changed += () => _changedCount++;
    }

    private static AppSettings WithDetail(LogDetail detail) => new() { Logging = new LoggingSettings { Detail = detail } };

    [Fact]
    public void DefaultSettings_AreInitiallyApplied()
    {
        Assert.Equal(new AppSettings(), _service.Settings);
        Assert.Equal(LogDetail.Normal, _service.Settings.Logging.Detail);
    }

    [Fact]
    public void Constructor_LoadsSavedSettings()
    {
        _store.Save(WithDetail(LogDetail.Detailed));

        var service = new AppSettingsService(_store);

        Assert.Equal(LogDetail.Detailed, service.Settings.Logging.Detail);
    }

    [Fact]
    public void Constructor_NormalizesSavedSettings()
    {
        _store.Save(WithDetail((LogDetail)99));

        var service = new AppSettingsService(_store);

        Assert.Equal(LogDetail.Normal, service.Settings.Logging.Detail);
    }

    [Fact]
    public void Update_AppliesAndSavesChange()
    {
        _service.Update(settings => settings with { Logging = settings.Logging with { Detail = LogDetail.WarningsOnly } });

        Assert.Equal(LogDetail.WarningsOnly, _service.Settings.Logging.Detail);
        Assert.Equal(_service.Settings, _store.Settings);
    }

    [Fact]
    public void Update_RaisesChangedOnce()
    {
        _service.Update(settings => WithDetail(LogDetail.Detailed));

        Assert.Equal(1, _changedCount);
    }

    [Fact]
    public void Update_WithNothingNew_DoesNotSaveOrRaiseChanged()
    {
        _service.Update(settings => settings with { });

        Assert.Equal(0, _changedCount);
        Assert.Equal(0, _store.SaveCount);
    }

    [Fact]
    public void Update_NormalizesTheChange()
    {
        _service.Update(settings => WithDetail((LogDetail)99));

        Assert.Equal(LogDetail.Normal, _service.Settings.Logging.Detail);
    }

    [Fact]
    public void Update_NullChange_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _service.Update(null!));
    }
}
