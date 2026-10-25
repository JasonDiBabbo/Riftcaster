using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Riftcaster.Contracts;
using Riftcaster.Core.Settings;
using Riftcaster.Core.Updates;

namespace Riftcaster.Core.Tests;

// What the Settings page and the tray know about updating: the mode and channel, how far it's got, and what can be
// asked of it. The updater that fetches is a fake that the test controls.
public class UpdateServiceTests
{
    private sealed class FakeFetcher : IUpdateFetcher
    {
        public string? DownloadedVersion { get; set; }

        // What a check finds: a version, or null for none. Throws if set to.
        public string? Finds { get; set; }

        public Exception? Fails { get; set; }

        public TaskCompletionSource? Holds { get; set; }

        public List<UpdateChannel> Checked { get; } = [];

        public async Task<string?> CheckAndDownloadAsync(UpdateChannel channel, CancellationToken cancellationToken)
        {
            Checked.Add(channel);
            if (Holds is not null)
            {
                await Holds.Task;
            }

            return Fails is not null ? throw Fails : Finds;
        }
    }

    private readonly AppSettingsService _settings = new(new InMemoryAppSettingsStore());

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero));

    private readonly FakeLogger<UpdateService> _logger = new();

    private readonly FakeFetcher _fetcher = new();

    private int _restarts;

    private UpdateService Create(string version = "0.2.0", UpdateAvailability availability = UpdateAvailability.FromSource) =>
        new(_settings, version, availability, _time, _logger);

    private UpdateService Attached(string version = "0.2.0")
    {
        var service = Create(version);
        service.Attach(_fetcher, () => _restarts++);
        return service;
    }

    private void Choose(UpdateMode mode) => _settings.Update(settings => settings with { Updates = settings.Updates with { Mode = mode } });

    [Fact]
    public void WithoutAnUpdater_ThereIsNothingToUpdateAndItSaysWhy()
    {
        var service = Create(availability: UpdateAvailability.ConsoleInInstalledFolder);

        Assert.Equal(UpdateAvailability.ConsoleInInstalledFolder, service.Availability);
        Assert.False(service.CanCheck);
        Assert.False(service.CanRestartToUpdate);
        Assert.Equal(UpdatePhase.NotChecked, service.Status.Phase);
    }

    [Fact]
    public async Task WithoutAnUpdater_CheckingDoesNothing()
    {
        var service = Create();

        await service.CheckAsync();

        Assert.Equal(UpdatePhase.NotChecked, service.Status.Phase);
    }

    [Fact]
    public void AnUpdater_MakesItUpdatable()
    {
        var service = Attached();

        Assert.Equal(UpdateAvailability.Updatable, service.Availability);
        Assert.True(service.CanCheck);
    }

    [Fact]
    public void AnUpdateDownloadedEarlier_IsReadyAtOnce()
    {
        _fetcher.DownloadedVersion = "0.3.0";

        var service = Attached();

        Assert.Equal(UpdatePhase.Ready, service.Status.Phase);
        Assert.Equal("0.3.0", service.Status.ReadyVersion);
        Assert.True(service.CanRestartToUpdate);
    }

    [Fact]
    public void TheUpdaterCanSayItCant()
    {
        var service = Create();
        var changes = 0;
        service.Changed += () => changes++;

        service.Unavailable(UpdateAvailability.TurnedOffInConfiguration);

        Assert.Equal(UpdateAvailability.TurnedOffInConfiguration, service.Availability);
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task ANewestVersion_IsUpToDate_AndWhenItWasChecked()
    {
        var service = Attached();

        await service.CheckAsync();

        Assert.Equal(UpdatePhase.UpToDate, service.Status.Phase);
        Assert.Equal(_time.GetUtcNow(), service.Status.CheckedAt);
        Assert.Null(service.Status.ReadyVersion);
        Assert.True(service.CanCheck); // Again whenever asked
    }

    // Nothing else says a check happened and found nothing, which is what to look for when one seems not to have.
    [Fact]
    public async Task ACheck_IsLogged_WhateverItFinds()
    {
        var service = Attached();

        await service.CheckAsync();

        var messages = _logger.Collector.GetSnapshot().Select(record => record.Message).ToList();
        Assert.Contains("Looking for a newer version than 0.2.0 (Stable).", messages);
        Assert.Contains("Riftcaster 0.2.0 is the newest version.", messages);
    }

    [Fact]
    public async Task ANewerVersion_IsDownloadedAndReady()
    {
        _fetcher.Finds = "0.3.0";
        var service = Attached();

        await service.CheckAsync();

        Assert.Equal(UpdatePhase.Ready, service.Status.Phase);
        Assert.Equal("0.3.0", service.Status.ReadyVersion);
        Assert.Contains(_logger.Collector.GetSnapshot(), record => record.Message.Contains("0.3.0 is ready"));
        Assert.False(service.CanCheck); // Nothing newer is fetched until this one is installed
    }

    [Fact]
    public async Task AFailedCheck_IsFailedAndLogged_AndTriedAgainNextTime()
    {
        _fetcher.Fails = new HttpRequestException("No such host");
        var service = Attached();

        await service.CheckAsync();

        Assert.Equal(UpdatePhase.Failed, service.Status.Phase);
        Assert.Equal(_time.GetUtcNow(), service.Status.CheckedAt);
        Assert.Contains(_logger.Collector.GetSnapshot(), record => record.Level == LogLevel.Warning);

        _fetcher.Fails = null;
        await service.CheckAsync();
        Assert.Equal(UpdatePhase.UpToDate, service.Status.Phase);
    }

    // Stopped, as when Riftcaster quits: not a failure, and not left looking busy.
    [Fact]
    public async Task AStoppedCheck_IsNotAFailure_AndLooksAsItDidBefore()
    {
        using var stop = new CancellationTokenSource();
        await stop.CancelAsync();
        _fetcher.Fails = new OperationCanceledException(stop.Token);
        var service = Attached();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.CheckAsync(stop.Token));

        Assert.Equal(UpdatePhase.NotChecked, service.Status.Phase);
        Assert.DoesNotContain(_logger.Collector.GetSnapshot(), record => record.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task WhileChecking_AnotherCheckDoesNothing()
    {
        _fetcher.Holds = new TaskCompletionSource();
        var service = Attached();

        var first = service.CheckAsync();
        Assert.Equal(UpdatePhase.Checking, service.Status.Phase);
        Assert.False(service.CanCheck);
        await service.CheckAsync();
        _fetcher.Holds.SetResult();
        await first;

        Assert.Single(_fetcher.Checked);
        Assert.Equal(UpdatePhase.UpToDate, service.Status.Phase);
    }

    [Fact]
    public async Task AReadyUpdate_IsNotCheckedForAgain()
    {
        _fetcher.DownloadedVersion = "0.3.0";
        var service = Attached();

        await service.CheckAsync();

        Assert.Empty(_fetcher.Checked);
    }

    [Fact]
    public async Task WhenOff_NothingIsChecked()
    {
        Choose(UpdateMode.Off);
        var service = Attached();

        await service.CheckAsync();

        Assert.Empty(_fetcher.Checked);
        Assert.False(service.CanCheck);
    }

    [Fact]
    public async Task TurningUpdatesBackOn_ChecksAtOnce()
    {
        Choose(UpdateMode.Off);
        var service = Attached();
        var seen = new TaskCompletionSource();
        service.Changed += () =>
        {
            if (service.Status.Phase == UpdatePhase.UpToDate)
            {
                seen.TrySetResult();
            }
        };

        Choose(UpdateMode.AskFirst);

        await seen.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Single(_fetcher.Checked);
    }

    [Fact]
    public async Task ChangingBetweenTheOnModes_DoesNotCheck()
    {
        var service = Attached();

        Choose(UpdateMode.AskFirst);
        Choose(UpdateMode.Automatic);
        await Task.Delay(50);

        Assert.Empty(_fetcher.Checked);
        Assert.Equal(UpdatePhase.NotChecked, service.Status.Phase);
    }

    [Theory]
    [InlineData(UpdateMode.Automatic, true, true)]
    [InlineData(UpdateMode.AskFirst, true, false)]
    [InlineData(UpdateMode.Off, false, false)]
    public void WhatADownloadedUpdateCanDo_DependsOnTheMode(UpdateMode mode, bool restarts, bool installsOnQuit)
    {
        _fetcher.DownloadedVersion = "0.3.0";
        Choose(mode);
        var service = Attached();

        Assert.Equal(restarts, service.CanRestartToUpdate);
        Assert.Equal(installsOnQuit, service.InstallsOnQuit);
    }

    [Fact]
    public void WithNothingDownloaded_NothingInstalls()
    {
        var service = Attached();

        Assert.False(service.CanRestartToUpdate);
        Assert.False(service.InstallsOnQuit);
    }

    [Fact]
    public void RestartToUpdate_AsksTheLauncher_OnlyWhenItCan()
    {
        var service = Attached();
        service.RestartToUpdate();
        Assert.Equal(0, _restarts); // Nothing downloaded

        _fetcher.DownloadedVersion = "0.3.0";
        var ready = Attached();
        ready.RestartToUpdate();
        Assert.Equal(1, _restarts);

        Choose(UpdateMode.Off);
        ready.RestartToUpdate();
        Assert.Equal(1, _restarts); // Off, so it wouldn't be installed
    }

    [Fact]
    public void TheChannel_IsTheOneChosen()
    {
        var service = Attached();
        Assert.Equal(UpdateChannel.Stable, service.Channel);

        _settings.Update(settings => settings with { Updates = settings.Updates with { Channel = UpdateChannel.PreRelease } });

        Assert.Equal(UpdateChannel.PreRelease, service.Channel);
    }

    // Until one is chosen, a build keeps to the kind of release it came from.
    [Theory]
    [InlineData("0.2.0", UpdateChannel.Stable)]
    [InlineData("0.3.0-beta.1", UpdateChannel.PreRelease)]
    public void WithNoChannelChosen_ItFollowsTheRunningBuild(string version, UpdateChannel expected)
    {
        Assert.Equal(expected, Attached(version).Channel);
    }

    [Fact]
    public async Task ACheckLooksAtTheChannel()
    {
        _settings.Update(settings => settings with { Updates = settings.Updates with { Channel = UpdateChannel.PreRelease } });
        var service = Attached();

        await service.CheckAsync();

        Assert.Equal([UpdateChannel.PreRelease], _fetcher.Checked);
    }

    [Fact]
    public void ChangedIsRaised_WhenTheSettingsChange()
    {
        var service = Attached();
        var changes = 0;
        service.Changed += () => changes++;

        Choose(UpdateMode.AskFirst);

        Assert.Equal(1, changes);
        Assert.Equal(UpdateMode.AskFirst, service.Mode);
    }
}
