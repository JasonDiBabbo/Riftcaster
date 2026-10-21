using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Riftcaster.Contracts;
using Riftcaster.Core.Settings;

namespace Riftcaster.Server.Tests;

public class LogDetailSourceTests
{
    private const string Key = "Logging:LogLevel:Default";

    private readonly ConfigurationManager _configuration = new();

    private readonly AppSettingsService _settings = new(new InMemoryAppSettingsStore());

    private static void SetDetail(AppSettingsService settings, LogDetail detail) =>
        settings.Update(current => current with { Logging = current.Logging with { Detail = detail } });

    [Theory]
    [InlineData(LogDetail.WarningsOnly, LogLevel.Warning)]
    [InlineData(LogDetail.Normal, LogLevel.Information)]
    [InlineData(LogDetail.Detailed, LogLevel.Debug)]
    public void LevelOf_IsTheLevelForTheDetail(LogDetail detail, LogLevel level)
    {
        Assert.Equal(level, LogDetailSource.LevelOf(detail));
    }

    [Fact]
    public void Attach_SuppliesTheSavedDetail()
    {
        SetDetail(_settings, LogDetail.Detailed);
        var source = LogDetailSource.AddTo(_configuration);

        source.Attach(_settings);

        Assert.Equal("Debug", _configuration[Key]);
    }

    [Fact]
    public void Change_UpdatesTheConfigurationAndSignalsIt()
    {
        var source = LogDetailSource.AddTo(_configuration);
        source.Attach(_settings);
        var signaled = false;
        using var _ = ChangeToken.OnChange(((IConfiguration)_configuration).GetReloadToken, () => signaled = true);

        SetDetail(_settings, LogDetail.WarningsOnly);

        Assert.Equal("Warning", _configuration[Key]);
        Assert.True(signaled);
    }

    [Fact]
    public void SavedDetail_BeatsTheAppsettingsFiles()
    {
        _configuration.AddInMemoryCollection(new Dictionary<string, string?> { [Key] = "Error" }); // As appsettings.json would
        var source = LogDetailSource.AddTo(_configuration);

        source.Attach(_settings);

        Assert.Equal("Information", _configuration[Key]);
    }

    [Fact]
    public void LaunchOptions_BeatTheSavedDetail()
    {
        _configuration.AddInMemoryCollection(new Dictionary<string, string?> { [Key] = "Information" });
        _configuration.AddCommandLine(["--Logging:LogLevel:Default", "Error"]);
        var source = LogDetailSource.AddTo(_configuration);
        source.Attach(_settings);

        SetDetail(_settings, LogDetail.Detailed);

        Assert.Equal("Error", _configuration[Key]);
    }

    [Fact]
    public void BeforeAttach_SuppliesNothing()
    {
        _configuration.AddInMemoryCollection(new Dictionary<string, string?> { [Key] = "Error" });
        LogDetailSource.AddTo(_configuration);

        Assert.Equal("Error", _configuration[Key]);
    }
}
