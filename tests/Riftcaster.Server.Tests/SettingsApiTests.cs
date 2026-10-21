using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Riftcaster.Contracts;

namespace Riftcaster.Server.Tests;

// Its own test server (a fixture per class): these tests change the settings. Each test sets up
// what it relies on first, since they share that state.
public class SettingsApiTests(RiftcasterWebApplicationFactory factory) : IClassFixture<RiftcasterWebApplicationFactory>
{
    private readonly RestApiClient _api = new(factory.CreateClient());

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
