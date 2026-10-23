using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Riftcaster.Contracts;
using Riftcaster.Core.Network;
using Riftcaster.Core.Settings;

namespace Riftcaster.Core.Tests;

// Moving the running server to another port, and making sure it worked. The server's answer on a port
// (whether it's really this server) is given by the test, and the waits are short.
public class PortSwitcherTests
{
    private const int Old = 5062;

    private readonly NetworkAccess _network = new(false, [NetworkAccess.DefaultUrl]);

    private readonly InMemoryAppSettingsStore _store = new();

    private readonly AppSettingsService _settings;

    private readonly FakeLogger<PortSwitcher> _logger = new();

    private readonly List<int> _asked = [];

    // Which ports answer as this server. Starts with the one it's on, and the new one once the server follows.
    private Func<int, bool> _answers;

    private readonly PortSwitcher _switcher;

    public PortSwitcherTests()
    {
        _settings = new AppSettingsService(_store);
        _answers = port => port == _network.ListenUrls[0].Port; // Where it listens, it answers
        _switcher = new PortSwitcher(
            _network,
            _settings,
            (port, _) =>
            {
                _asked.Add(port);
                return Task.FromResult(_answers(port));
            },
            TimeProvider.System,
            _logger,
            grace: TimeSpan.FromMilliseconds(1),
            timeout: TimeSpan.FromMilliseconds(150),
            poll: TimeSpan.FromMilliseconds(5));
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    public async Task Switch_MovesTheServerAndSavesTheChoice()
    {
        var port = FreePort();

        var result = await _switcher.SwitchAsync(port);

        Assert.True(result.Switched);
        Assert.Null(result.Problem);
        Assert.Equal(port, _network.ListenUrls[0].Port);
        Assert.Equal(port, _switcher.RunningPort);
        Assert.Equal(port, _settings.Settings.Server.Port);
    }

    // Another program that takes the connections but never answers: the request times out, which HttpClient
    // reports as a cancellation. That's no answer, and the way back must still be taken.
    [Fact]
    public async Task Switch_WhenTheNewPortTimesOut_GoesBack()
    {
        var port = FreePort();
        var switcher = new PortSwitcher(
            _network,
            _settings,
            (asked, _) => asked == port ? throw new TaskCanceledException("The request timed out.") : Task.FromResult(asked == _network.ListenUrls[0].Port),
            TimeProvider.System,
            _logger,
            grace: TimeSpan.FromMilliseconds(1),
            timeout: TimeSpan.FromMilliseconds(150),
            poll: TimeSpan.FromMilliseconds(5));

        var result = await switcher.SwitchAsync(port);

        Assert.False(result.Switched);
        Assert.Equal(Old, _network.ListenUrls[0].Port);
        Assert.Null(_settings.Settings.Server.Port);
        Assert.Null(switcher.SwitchingTo);
    }

    [Fact]
    public async Task Switch_ToTheDefaultPort_SavesNoChoice()
    {
        var other = FreePort();
        await _switcher.SwitchAsync(other);

        var result = await _switcher.SwitchAsync(ListenPort.Default);

        Assert.True(result.Switched);
        Assert.Null(_settings.Settings.Server.Port); // The default is none, so a copy that never picked follows it
        Assert.Equal(ListenPort.Default, _switcher.RunningPort);
    }

    [Fact]
    public async Task Switch_TellsThePagesBeforeTheOldPortCloses()
    {
        var port = FreePort();
        var portWhenTold = 0;
        var told = 0;
        _switcher.Moving += moving =>
        {
            told = moving;
            portWhenTold = _network.ListenUrls[0].Port; // Still the old one: the pages need it to hear
        };

        await _switcher.SwitchAsync(port);

        Assert.Equal(port, told);
        Assert.Equal(Old, portWhenTold);
    }

    [Fact]
    public async Task Switch_RaisesChangedWhenItStartsAndWhenItEnds()
    {
        var port = FreePort();
        var seen = new List<int?>();
        _switcher.Changed += () => seen.Add(_switcher.SwitchingTo);

        await _switcher.SwitchAsync(port);

        Assert.Equal([port, null], seen);
        Assert.Null(_switcher.SwitchingTo);
    }

    [Fact]
    public async Task Switch_AsksTheNewPortWhoAnswers_NotTheOld()
    {
        var port = FreePort();

        await _switcher.SwitchAsync(port);

        Assert.All(_asked, asked => Assert.Equal(port, asked));
        Assert.NotEmpty(_asked);
    }

    // Closing the old port takes a few seconds, so the new one isn't up at once: it keeps asking.
    [Fact]
    public async Task Switch_WaitsForTheNewPortToAnswer()
    {
        var port = FreePort();
        var calls = 0;
        _answers = asked => asked == port && ++calls >= 4;

        // Plenty of time: a slow machine must not run out of it before the fourth ask.
        var switcher = new PortSwitcher(
            _network,
            _settings,
            (asked, _) => Task.FromResult(_answers(asked)),
            TimeProvider.System,
            _logger,
            grace: TimeSpan.FromMilliseconds(1),
            timeout: TimeSpan.FromSeconds(30),
            poll: TimeSpan.FromMilliseconds(5));

        var result = await switcher.SwitchAsync(port);

        Assert.True(result.Switched);
        Assert.True(calls >= 4);
    }

    [Fact]
    public async Task Switch_ToAPortSomeoneHolds_IsRefusedAndChangesNothing()
    {
        using var holder = new TcpListener(IPAddress.Loopback, 0);
        holder.Start();
        var port = ((IPEndPoint)holder.LocalEndpoint).Port;
        var told = false;
        _switcher.Moving += _ => told = true;

        var result = await _switcher.SwitchAsync(port);

        Assert.False(result.Switched);
        Assert.Equal($"Port {port} is in use by another program.", result.Problem);
        Assert.Equal(Old, _network.ListenUrls[0].Port);
        Assert.False(told); // Nobody was told to move
        Assert.Empty(_asked);
        Assert.Null(_settings.Settings.Server.Port);
    }

    // Another program has the port, or it can't be bound, though the check said it was free: the server is left
    // listening on nothing, which is the one outcome that can't be allowed. It goes back.
    [Fact]
    public async Task Switch_WhenTheNewPortNeverAnswers_GoesBackToTheOldOne()
    {
        var port = FreePort();
        _answers = asked => asked == _network.ListenUrls[0].Port && asked == Old; // Only the old port ever works

        var result = await _switcher.SwitchAsync(port);

        Assert.False(result.Switched);
        Assert.Equal($"Couldn't start on port {port}, so Riftcaster is back on port {Old}.", result.Problem);
        Assert.Equal(Old, _network.ListenUrls[0].Port);
        Assert.Equal(Old, _switcher.RunningPort);
        Assert.Contains(_logger.Collector.GetSnapshot(), record => record.Level == LogLevel.Error);
    }

    [Fact]
    public async Task Switch_ThatFailed_LeavesTheChoiceAsItWas()
    {
        _settings.Update(settings => settings with { Server = new ServerSettings { Port = 5099 } });
        var port = FreePort();
        _answers = asked => asked == Old && _network.ListenUrls[0].Port == Old;

        await _switcher.SwitchAsync(port);

        // The choice follows the port in use: a restart comes back to a port that works.
        Assert.Equal(Old, ListenPort.Default);
        Assert.Null(_settings.Settings.Server.Port);
    }

    [Fact]
    public async Task Switch_WhenItCantGoBackEither_SaysToRestart()
    {
        var port = FreePort();
        _answers = _ => false;

        var result = await _switcher.SwitchAsync(port);

        Assert.False(result.Switched);
        Assert.Equal($"Couldn't start on port {port}, and couldn't go back to port {Old}. Restart Riftcaster.", result.Problem);
        Assert.Contains(_logger.Collector.GetSnapshot(), record => record.Level == LogLevel.Critical);
        Assert.Null(_switcher.SwitchingTo); // Not left looking busy
    }

    [Fact]
    public async Task Switch_ThatFailed_RaisesChangedAtTheEndAsWell()
    {
        var port = FreePort();
        _answers = asked => asked == Old && _network.ListenUrls[0].Port == Old;
        var seen = new List<int?>();
        _switcher.Changed += () => seen.Add(_switcher.SwitchingTo);

        await _switcher.SwitchAsync(port);

        Assert.Equal([port, null], seen);
    }

    // What answers is asked by HTTP, which throws when nothing is listening yet: the same as no, and asked again.
    [Fact]
    public async Task Switch_TreatsAnErrorWhileAskingAsNotYet()
    {
        var port = FreePort();
        var calls = 0;
        var switcher = new PortSwitcher(
            _network,
            _settings,
            (asked, _) => ++calls < 3 ? throw new HttpRequestException("Connection refused") : Task.FromResult(asked == port),
            TimeProvider.System,
            _logger,
            TimeSpan.FromMilliseconds(1),
            TimeSpan.FromSeconds(30), // Plenty: a slow machine must not run out before the third ask
            TimeSpan.FromMilliseconds(5));

        var result = await switcher.SwitchAsync(port);

        Assert.True(result.Switched);
        Assert.True(calls >= 3);
    }

    [Fact]
    public async Task Switch_ToThePortInUse_DoesNothing()
    {
        var told = false;
        _switcher.Moving += _ => told = true;

        var result = await _switcher.SwitchAsync(Old);

        Assert.True(result.Switched);
        Assert.False(told);
        Assert.Empty(_asked);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(80)]
    [InlineData(1023)]
    [InlineData(65536)]
    [InlineData(-5)]
    public async Task Switch_ToAPortThatCantBeChosen_IsRefused(int port)
    {
        var result = await _switcher.SwitchAsync(port);

        Assert.False(result.Switched);
        Assert.Equal($"Use a port from {ListenPort.Min} to {ListenPort.Max}.", result.Problem);
        Assert.Equal(Old, _network.ListenUrls[0].Port);
    }

    // A developer's setup with only https addresses has no HTTP port to move.
    [Fact]
    public async Task Switch_WithOnlyHttpsAddresses_IsRefused()
    {
        var network = new NetworkAccess(false, [new Uri("https://localhost:7216")]);
        var switcher = new PortSwitcher(network, _settings, (_, _) => Task.FromResult(true), TimeProvider.System, _logger);

        var result = await switcher.SwitchAsync(FreePort());

        Assert.False(result.Switched);
        Assert.Equal("This run has no HTTP port to move.", result.Problem);
        Assert.Null(switcher.RunningPort);
    }

    // Closing the port while one is closing the port would race: the second waits for the first by being refused.
    [Fact]
    public async Task Switch_WhileAnotherIsMoving_IsRefused()
    {
        var first = FreePort();
        var second = FreePort();
        var release = new TaskCompletionSource();
        var switcher = new PortSwitcher(
            _network,
            _settings,
            async (asked, _) =>
            {
                await release.Task;
                return asked == first;
            },
            TimeProvider.System,
            _logger,
            TimeSpan.FromMilliseconds(1),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMilliseconds(5));
        var moving = switcher.SwitchAsync(first);
        while (switcher.SwitchingTo is null)
        {
            await Task.Delay(1);
        }

        var result = await switcher.SwitchAsync(second);
        release.SetResult();
        var firstResult = await moving;

        Assert.False(result.Switched);
        Assert.Equal("Riftcaster is already switching ports.", result.Problem);
        Assert.True(firstResult.Switched);
        Assert.Equal(first, _network.ListenUrls[0].Port);
    }

    [Fact]
    public async Task Switch_CanBeCancelled()
    {
        var port = FreePort();
        using var cancellation = new CancellationTokenSource();
        var switcher = new PortSwitcher(
            _network,
            _settings,
            async (_, token) =>
            {
                await cancellation.CancelAsync();
                token.ThrowIfCancellationRequested();
                return false;
            },
            TimeProvider.System,
            _logger,
            TimeSpan.FromMilliseconds(1),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMilliseconds(5));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => switcher.SwitchAsync(port, cancellation.Token));

        Assert.Null(switcher.SwitchingTo); // Not left looking busy
    }

    [Fact]
    public void RunningPort_IsTheHttpOne()
    {
        var network = new NetworkAccess(false, [new Uri("https://localhost:7216"), new Uri("http://localhost:5062")]);
        var switcher = new PortSwitcher(network, _settings, (_, _) => Task.FromResult(true), TimeProvider.System, _logger);

        Assert.Equal(5062, switcher.RunningPort);
    }

    [Fact]
    public void TheDefaultTimings_AreLongEnoughForTheOldPortToClose()
    {
        // Moving takes a few seconds while anything is connected, so it waits much longer than that.
        Assert.True(PortSwitcher.DefaultTimeout >= TimeSpan.FromSeconds(10));
        Assert.True(PortSwitcher.DefaultGrace >= TimeSpan.FromMilliseconds(500));
        Assert.True(PortSwitcher.DefaultPoll <= TimeSpan.FromSeconds(1));
    }
}
