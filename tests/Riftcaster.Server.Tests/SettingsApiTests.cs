using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Riftcaster.Contracts;
using Riftcaster.Core.Diagnostics;

namespace Riftcaster.Server.Tests;

// Its own test server (a fixture per class): these tests change the settings. Each test sets up
// what it relies on first, since they share that state.
public class SettingsApiTests(RiftcasterWebApplicationFactory factory) : IClassFixture<RiftcasterWebApplicationFactory>
{
    private readonly RestApiClient _api = new(factory.CreateClient());

    // What the Settings page's Open folder button opens: the same folder the log files are written to.
    [Fact]
    public void LogFolderOpener_PointsAtTheLogsFolderInTheDataFolder()
    {
        var opener = factory.Services.GetRequiredService<LogFolderOpener>();

        Assert.Equal(factory.Services.GetRequiredService<LogFolder>().Path, opener.FolderPath);
        Assert.Equal(LogFiles.FolderName, Path.GetFileName(opener.FolderPath));
    }

    // The match defaults: the first change starts from the design's, and only the fields sent change.
    [Fact]
    public async Task PatchSettings_ChangesOnlyTheMatchDefaultsFieldsSent()
    {
        await _api.SendAsync<AppSettings>(HttpMethod.Patch, "/api/settings", """{ "matchDefaults": { "mode": "OneVsOne", "format": "BestOf3", "pointsToWin": 8, "durationMinutes": 50, "allowOvertime": true } }""");

        var settings = await _api.SendAsync<AppSettings>(HttpMethod.Patch, "/api/settings", """{ "matchDefaults": { "mode": "TwoVsTwo", "pointsToWin": 11 } }""");

        var defaults = settings.MatchDefaults!;
        Assert.Equal(MatchMode.TwoVsTwo, defaults.Mode);
        Assert.Equal(11, defaults.PointsToWin);
        Assert.Equal(MatchFormat.BestOf3, defaults.Format); // Not sent, so kept
        Assert.Equal(50, defaults.DurationMinutes);
        Assert.True(defaults.AllowOvertime);
        Assert.Equal(settings, await _api.GetAsync<AppSettings>("/api/settings"));
    }

    [Theory]
    [InlineData("""{ "matchDefaults": { "mode": 9 } }""", "matchDefaults.mode")]
    [InlineData("""{ "matchDefaults": { "format": 5 } }""", "matchDefaults.format")]
    [InlineData("""{ "matchDefaults": { "pointsToWin": 7 } }""", "pointsToWin")]
    [InlineData("""{ "matchDefaults": { "durationMinutes": 181 } }""", "durationMinutes")]
    public async Task PatchSettings_InvalidMatchDefault_Is400(string body, string field)
    {
        var errors = await _api.InvalidAsync(HttpMethod.Patch, "/api/settings", body);

        Assert.Contains(errors.Keys, key => key.EndsWith(field, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PatchSettings_UnknownMatchDefaultModeName_Is400()
    {
        var response = await _api.SendRawAsync(HttpMethod.Patch, "/api/settings", """{ "matchDefaults": { "mode": "OneVsTen" } }""");

        Assert.Equal(400, (int)response.StatusCode);
    }

    [Fact]
    public async Task PatchSettings_WithoutMatchDefaults_KeepsThem()
    {
        await _api.SendAsync<AppSettings>(HttpMethod.Patch, "/api/settings", """{ "matchDefaults": { "pointsToWin": 12 } }""");

        var settings = await _api.SendAsync<AppSettings>(HttpMethod.Patch, "/api/settings", """{ "logging": { "detail": "Normal" } }""");

        Assert.Equal(12, settings.MatchDefaults!.PointsToWin);
    }

    // Resetting the match gives it the chosen defaults and keeps what isn't a rule: the players.
    [Fact]
    public async Task ResetMatchToDefaults_GivesTheMatchTheChosenDefaults()
    {
        await _api.SendAsync<AppSettings>(HttpMethod.Patch, "/api/settings", """{ "matchDefaults": { "mode": "FreeForAll3", "format": "BestOf1", "pointsToWin": 11, "durationMinutes": 35, "allowOvertime": false } }""");
        await _api.SendAsync<MatchSettings>(HttpMethod.Patch, "/api/match", """{ "mode": "OneVsOne", "format": "BestOf3", "pointsToWin": 8, "durationMinutes": 50, "allowOvertime": true }""");

        var match = await _api.SendAsync<MatchSettings>(HttpMethod.Post, "/api/match/reset-to-defaults");

        Assert.Equal(new MatchSettings(11, MatchFormat.BestOf1, MatchMode.FreeForAll3, 35, false), match);
        Assert.Equal(match, await _api.GetAsync<MatchSettings>("/api/match"));
    }

    [Fact]
    public async Task PatchMatch_StillRejectsAnInvalidMode()
    {
        // The match's own validation, now shared with the match defaults'.
        var errors = await _api.InvalidAsync(HttpMethod.Patch, "/api/match", """{ "mode": 9 }""");

        Assert.Contains("mode", errors.Keys);
    }

    [Fact]
    public async Task PatchSettings_ChangesTheLogDetail()
    {
        var settings = await _api.SendAsync<AppSettings>(HttpMethod.Patch, "/api/settings", """{ "logging": { "detail": "Detailed" } }""");

        Assert.Equal(LogDetail.Detailed, settings.Logging.Detail);
        Assert.Equal(settings, await _api.GetAsync<AppSettings>("/api/settings"));
    }

    [Fact]
    public async Task PatchSettings_ChangesOnlyTheFieldsSent()
    {
        await _api.SendAsync<AppSettings>(HttpMethod.Patch, "/api/settings", """{ "logging": { "detail": "WarningsOnly" } }""");

        await _api.SendAsync<AppSettings>(HttpMethod.Patch, "/api/settings", "{}");
        await _api.SendAsync<AppSettings>(HttpMethod.Patch, "/api/settings", """{ "logging": {} }""");

        var settings = await _api.GetAsync<AppSettings>("/api/settings");
        Assert.Equal(LogDetail.WarningsOnly, settings.Logging.Detail); // Not sent, so kept
    }

    [Fact]
    public async Task PatchSettings_InvalidDetail_Is400()
    {
        var errors = await _api.InvalidAsync(HttpMethod.Patch, "/api/settings", """{ "logging": { "detail": 9 } }""");

        Assert.Contains("logging.detail", errors.Keys);
    }

    [Fact]
    public async Task PatchSettings_UnknownDetailName_Is400()
    {
        var response = await _api.SendRawAsync(HttpMethod.Patch, "/api/settings", """{ "logging": { "detail": "Chatty" } }""");

        Assert.Equal(400, (int)response.StatusCode);
    }

    // The log levels follow the setting while the server runs: no restart.
    [Fact]
    public async Task PatchSettings_ChangesWhatTheServerLogsAtOnce()
    {
        var logger = factory.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Riftcaster.Tests");

        await _api.SendAsync<AppSettings>(HttpMethod.Patch, "/api/settings", """{ "logging": { "detail": "Detailed" } }""");
        Assert.Equal("Debug", factory.Services.GetRequiredService<IConfiguration>()["Logging:LogLevel:Default"]);
        Assert.True(logger.IsEnabled(LogLevel.Debug));

        await _api.SendAsync<AppSettings>(HttpMethod.Patch, "/api/settings", """{ "logging": { "detail": "WarningsOnly" } }""");
        Assert.False(logger.IsEnabled(LogLevel.Information));
        Assert.True(logger.IsEnabled(LogLevel.Warning));

        await _api.SendAsync<AppSettings>(HttpMethod.Patch, "/api/settings", """{ "logging": { "detail": "Normal" } }""");
        Assert.False(logger.IsEnabled(LogLevel.Debug));
        Assert.True(logger.IsEnabled(LogLevel.Information));
    }
}
