using System.Net;
using System.Net.Sockets;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Riftcaster.Contracts;
using Riftcaster.Core.Network;
using Riftcaster.Core.Settings;
using Riftcaster.WebUI.Layout;

namespace Riftcaster.WebUI.Tests;

// The notice on every page of the web interface while Riftcaster moves to another port: it says so, and its script
// takes the page to the new address, which the page does by itself, as the connection to the old port closes.
public class PortMoveNoticeTests : BunitContext
{
    private const string Module = "./_content/Riftcaster.WebUI/Layout/PortMoveNotice.razor.js";

    private static readonly ServerIdentity Identity = new("Riftcaster Server", "0.2.1", new DateTimeOffset(2026, 10, 22, 5, 53, 57, TimeSpan.FromHours(-7)).AddTicks(3470599));

    private readonly NetworkAccess _network = new(false, [NetworkAccess.DefaultUrl]);

    private readonly BunitJSModuleInterop _script;

    // Which ports answer as this server: where it's listening, until a test says otherwise.
    private Func<int, Task<bool>> _answers;

    private readonly PortSwitcher _switcher;

    public PortMoveNoticeTests()
    {
        _answers = port => Task.FromResult(port == _network.ListenUrls[0].Port);
        var settings = new AppSettingsService(new InMemoryAppSettingsStore());
        _switcher = new PortSwitcher(
            _network,
            settings,
            (port, _) => _answers(port),
            TimeProvider.System,
            NullLogger<PortSwitcher>.Instance,
            grace: TimeSpan.FromMilliseconds(1),
            timeout: TimeSpan.FromMilliseconds(200),
            poll: TimeSpan.FromMilliseconds(5));
        Services.AddSingleton(_switcher);
        Services.AddSingleton(Identity);
        _script = JSInterop.SetupModule(Module);
        _script.Mode = JSRuntimeMode.Loose;
    }

    // How many times the page's script was called by that name.
    private int Calls(string function) => _script.Invocations[function].Count();

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    public void ShowsNothingUntilRiftcasterMoves()
    {
        var notice = Render<PortMoveNotice>();

        Assert.Empty(notice.FindAll(".moving"));
        Assert.Equal("", notice.Markup.Trim());
    }

    [Fact]
    public void WhenRiftcasterMoves_SaysSoAndWhereTheNewAddressIs()
    {
        var port = FreePort();
        var release = new TaskCompletionSource<bool>();
        var navigation = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        navigation.NavigateTo("http://localhost/settings?tab=server");
        var notice = Render<PortMoveNotice>();
        _answers = asked => asked == port ? release.Task : Task.FromResult(false);

        var moving = _switcher.SwitchAsync(port);

        notice.WaitForAssertion(() => Assert.Equal($"Riftcaster is moving to port {port}", notice.Find(".moving b").TextContent));
        var link = notice.Find(".moving a");
        Assert.Equal($"http://localhost:{port}/settings?tab=server", link.GetAttribute("href")); // The same page, on the new port
        Assert.Contains("a few seconds", notice.Find(".moving").TextContent);
        Assert.Equal("status", notice.Find(".moving").GetAttribute("role"));
        release.SetResult(true);
        moving.GetAwaiter().GetResult();
    }

    [Fact]
    public void ItsScriptIsToldToFollow_WithTheServersOwnStartTime()
    {
        var port = FreePort();
        var notice = Render<PortMoveNotice>();

        _switcher.SwitchAsync(port).GetAwaiter().GetResult();

        notice.WaitForAssertion(() =>
        {
            var call = Assert.Single(_script.Invocations["follow"]);
            Assert.Equal(port, call.Arguments[0]);
            // Which the new port must answer with, so the page never goes somewhere that isn't Riftcaster.
            Assert.Equal(Identity.StartTime.ToString("o"), call.Arguments[1]);
        });
    }

    // Moved, so it stays: the page is on its way to the new address, and shouldn't flicker back to normal.
    [Fact]
    public void WhenItHasMoved_TheNoticeStays()
    {
        var port = FreePort();
        var notice = Render<PortMoveNotice>();

        _switcher.SwitchAsync(port).GetAwaiter().GetResult();

        Assert.NotEmpty(notice.FindAll(".moving"));
        Assert.Equal(0, Calls("cancel"));
    }

    // It couldn't, and is back where it was: the page stays, and its script stops waiting for a port that isn't coming.
    [Fact]
    public void WhenItCouldntMove_TheNoticeGoesAndTheScriptStops()
    {
        var port = FreePort();
        var notice = Render<PortMoveNotice>();
        _answers = asked => Task.FromResult(asked == 5062 && _network.ListenUrls[0].Port == 5062);

        var result = _switcher.SwitchAsync(port).GetAwaiter().GetResult();

        Assert.False(result.Switched);
        notice.WaitForAssertion(() =>
        {
            Assert.Empty(notice.FindAll(".moving"));
            Assert.Single(_script.Invocations["cancel"]);
        });
    }

    [Fact]
    public void AnAddressThatIsRefused_ShowsNothing()
    {
        var notice = Render<PortMoveNotice>();

        // Refused before anyone is told to move: a port that can't be chosen.
        var result = _switcher.SwitchAsync(80).GetAwaiter().GetResult();

        Assert.False(result.Switched);
        Assert.Empty(notice.FindAll(".moving"));
        Assert.Equal(0, Calls("follow"));
    }

    [Fact]
    public async Task StopsListeningWhenRemoved()
    {
        Render<PortMoveNotice>();

        await DisposeComponentsAsync();

        // Would throw if the removed notice still tried to render, or to call its script.
        _switcher.SwitchAsync(FreePort()).GetAwaiter().GetResult();
        Assert.Equal(0, Calls("follow"));
    }

    [Fact]
    public void ASwitchOfAnotherCircuit_IsHeardByEveryOpenPage()
    {
        // Every open page of the web interface, whoever's, is told: each is its own notice.
        var first = Render<PortMoveNotice>();
        var second = Render<PortMoveNotice>();
        var port = FreePort();

        _switcher.SwitchAsync(port).GetAwaiter().GetResult();

        first.WaitForAssertion(() => Assert.NotEmpty(first.FindAll(".moving")));
        second.WaitForAssertion(() => Assert.NotEmpty(second.FindAll(".moving")));
        Assert.Equal(2, _script.Invocations["follow"].Count());
    }
}
