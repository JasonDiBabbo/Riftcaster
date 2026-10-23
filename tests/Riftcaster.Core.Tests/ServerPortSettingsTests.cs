using Microsoft.Extensions.Logging.Abstractions;
using Riftcaster.Contracts;
using Riftcaster.Core.Network;
using Riftcaster.Core.Settings;

namespace Riftcaster.Core.Tests;

// The port chosen on the Settings page: kept in the settings, and read by the server at its next start.
public sealed class ServerPortSettingsTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("riftcaster-tests-");

    private readonly InMemoryAppSettingsStore _store = new();

    private readonly AppSettingsService _settings;

    public ServerPortSettingsTests()
    {
        _settings = new AppSettingsService(_store);
    }

    public void Dispose()
    {
        _directory.Delete(recursive: true);
    }

    private static AppSettings WithPort(int port) => new() { Server = new ServerSettings { Port = port } };

    [Fact]
    public void ChosenPort_IsTheDefaultUntilOneIsChosen()
    {
        Assert.Null(_settings.Settings.Server.Port);
        Assert.Equal(ListenPort.Default, _settings.ChosenPort);
    }

    [Fact]
    public void ChoosePort_SavesIt()
    {
        _settings.ChoosePort(5070);

        Assert.Equal(5070, _settings.ChosenPort);
        Assert.Equal(5070, _store.Settings!.Server.Port);
    }

    // The default is none, so a copy that never picked one follows the default.
    [Fact]
    public void ChoosePort_TheDefault_ChoosesNone()
    {
        _settings.ChoosePort(5070);

        _settings.ChoosePort(ListenPort.Default);

        Assert.Null(_settings.Settings.Server.Port);
    }

    [Fact]
    public void ChosenPort_IsTheOneChosen_AndSaved()
    {
        _settings.Update(settings => settings with { Server = new ServerSettings { Port = 5070 } });

        Assert.Equal(5070, _settings.ChosenPort);
        Assert.Equal(5070, _store.Settings!.Server.Port);
    }

    [Fact]
    public void ChosenPort_ComesBackFromTheStore()
    {
        _store.Save(WithPort(5071));

        var service = new AppSettingsService(_store);

        Assert.Equal(5071, service.ChosenPort);
    }

    // The server ignores such a port at its start, so it's none here as well: the page never shows one
    // the server isn't going to use.
    [Theory]
    [InlineData(80)]
    [InlineData(0)]
    [InlineData(70000)]
    public void AnUnusablePort_IsNone(int port)
    {
        _store.Save(WithPort(port));

        var service = new AppSettingsService(_store);

        Assert.Null(service.Settings.Server.Port);
        Assert.Equal(ListenPort.Default, service.ChosenPort);
    }

    [Fact]
    public void ChoosingAnUnusablePort_IsChoosingNone()
    {
        _settings.Update(settings => WithPort(5070));

        _settings.Update(settings => WithPort(80));

        Assert.Null(_settings.Settings.Server.Port);
    }

    [Fact]
    public void ChoosingNone_ReturnsToTheDefault()
    {
        _settings.Update(settings => WithPort(5070));

        _settings.Update(settings => settings with { Server = new ServerSettings { Port = null } });

        Assert.Equal(ListenPort.Default, _settings.ChosenPort);
    }

    [Fact]
    public void ThePortIsSavedInAFileThatOlderVersionsDidntHave()
    {
        var path = Path.Combine(_directory.FullName, "settings.json");
        var store = new JsonFileAppSettingsStore(path, NullLogger<JsonFileAppSettingsStore>.Instance);

        store.Save(WithPort(5070));

        Assert.Equal(5070, new JsonFileAppSettingsStore(path, NullLogger<JsonFileAppSettingsStore>.Instance).Load()!.Server.Port);
        Assert.Contains("\"port\": 5070", File.ReadAllText(path));
    }

    // Saved before the port could be chosen: it has no server part, and must still load, with the default.
    [Fact]
    public void AFileSavedBeforeThePortCouldBeChosen_StillLoads()
    {
        var path = Path.Combine(_directory.FullName, "settings.json");
        File.WriteAllText(path, """{"logging":{"detail":"Detailed"}}""");

        var loaded = new JsonFileAppSettingsStore(path, NullLogger<JsonFileAppSettingsStore>.Instance).Load();

        Assert.NotNull(loaded);
        Assert.Equal(LogDetail.Detailed, loaded.Logging.Detail);
        Assert.Null(loaded.Server.Port);
        Assert.True(File.Exists(path));
    }
}
