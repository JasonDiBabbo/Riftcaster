using System.Net;
using System.Net.Sockets;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Riftcaster.Contracts;
using Riftcaster.Core.Network;
using Riftcaster.Core.Settings;
using Riftcaster.WebUI.Layout;
using Riftcaster.WebUI.Settings;

namespace Riftcaster.WebUI.Tests;

// The Settings page's server section: the port chosen, and moving Riftcaster to it while it runs.
public class ServerSectionTests : BunitContext
{
    private readonly AppSettingsService _settings = new(new InMemoryAppSettingsStore());

    private NetworkAccess _network = new(false, [NetworkAccess.DefaultUrl]);

    // Which ports answer as this server: where it's listening, until a test says otherwise.
    private Func<int, Task<bool>> _answers;

    private PortSwitcher _switcher = null!;

    public ServerSectionTests()
    {
        _answers = port => Task.FromResult(port == _network.ListenUrls[0].Port);
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    // How the server started is fixed for the run, so tests say so before rendering.
    private IRenderedComponent<ServerSection> RenderSection(bool local = true, PortSource source = PortSource.Default, params Uri[] listening)
    {
        if (listening.Length > 0)
        {
            _network = new NetworkAccess(false, listening);
        }

        _switcher = new PortSwitcher(
            _network,
            _settings,
            (port, _) => _answers(port),
            TimeProvider.System,
            NullLogger<PortSwitcher>.Instance,
            grace: TimeSpan.FromMilliseconds(1),
            timeout: TimeSpan.FromMilliseconds(200),
            poll: TimeSpan.FromMilliseconds(5));
        Services.AddSingleton(_settings);
        Services.AddSingleton(_network);
        Services.AddSingleton(_switcher);
        Services.AddSingleton(new PortOrigin(source));
        return Render<ServerSection>(parameters => parameters.AddCascadingValue(new OperatorSession(IsLocal: local, CodeVersion: null)));
    }

    private static AngleSharp.Dom.IElement Field(IRenderedComponent<ServerSection> section) => section.Find("input[aria-label='Server port']");

    private static string Text(IRenderedComponent<ServerSection> section, string selector) => section.Find(selector).TextContent.Trim();

    private void Choose(int port) =>
        _settings.Update(settings => settings with { Server = new ServerSettings { Port = port == ListenPort.Default ? null : port } });

    [Fact]
    public void ShowsThePortChosen_TheDefaultUntilOne()
    {
        var section = RenderSection();

        Assert.Equal("5062", Field(section).GetAttribute("value"));
        Assert.False(Field(section).HasAttribute("disabled"));
        Assert.Empty(section.FindAll(".notice"));
        Assert.Empty(section.FindAll(".problem"));
        Assert.Empty(section.FindAll(".explanation"));
    }

    [Fact]
    public void SaysWhatThePortCoversAndWhenItApplies()
    {
        var section = RenderSection();

        Assert.Equal("Server port", Text(section, ".row .title"));
        Assert.Equal("Applies when you switch, or at the next start", Text(section, ".control .note"));
    }

    // A row has a rule under it unless it's the last in its container; this one is the only thing in its
    // own, so nothing separates it from the boxes that follow.
    [Fact]
    public void ThePortRowHasNoRuleUnderIt()
    {
        var section = RenderSection();

        Assert.Equal(1, Field(section).Closest(".row")!.ParentElement!.ChildElementCount);
    }

    [Fact]
    public void ShowsAPortChosenBefore_WhichIsTheOneInUse()
    {
        var port = FreePort();
        Choose(port);

        var section = RenderSection(listening: new Uri($"http://localhost:{port}"));

        Assert.Equal(port.ToString(), Field(section).GetAttribute("value"));
        Assert.Empty(section.FindAll(".notice")); // Nothing to switch to
    }

    [Fact]
    public void ChoosingAPort_SavesItAndAsksWhetherToSwitch()
    {
        var port = FreePort();
        var section = RenderSection();

        Field(section).Change(port.ToString());

        Assert.Equal(port, _settings.Settings.Server.Port);
        Assert.Equal($"Switch Riftcaster to port {port}?", Text(section, ".notice b"));
        var text = Text(section, ".notice .text");
        Assert.Contains("keeps using port 5062 until it restarts", text);
        Assert.Contains($"Switch now to move to port {port}", text);
        Assert.Contains("Browser Sources are still using port 5062. Change them to", text);
        Assert.Contains($"http://localhost:{port}", text);
        Assert.Equal(["Undo", "Switch now"], section.FindAll(".notice button").Select(button => button.TextContent.Trim()));
        Assert.Equal(5062, _network.ListenUrls[0].Port); // Asking doesn't move anything
    }

    [Fact]
    public void TypingAroundThePort_IsIgnored()
    {
        var port = FreePort();
        var section = RenderSection();

        Field(section).Change($"  {port} ");

        Assert.Equal(port, _settings.Settings.Server.Port);
        Assert.Equal(port.ToString(), Field(section).GetAttribute("value"));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("80")]
    [InlineData("1023")]
    [InlineData("65536")]
    [InlineData("")]
    public void TypingSomethingThatIsntAUsablePort_SavesNothingAndSaysSo(string text)
    {
        var section = RenderSection();

        Field(section).Change(text);

        Assert.Null(_settings.Settings.Server.Port);
        Assert.Equal($"Use a port from {ListenPort.Min} to {ListenPort.Max}.", Text(section, ".problem"));
        Assert.Equal("true", Field(section).GetAttribute("aria-invalid"));
        Assert.Empty(section.FindAll(".notice"));
    }

    [Fact]
    public void ThenTypingAUsableOne_ClearsTheComplaint()
    {
        var port = FreePort();
        var section = RenderSection();
        Field(section).Change("abc");

        Field(section).Change(port.ToString());

        Assert.Empty(section.FindAll(".problem"));
        Assert.Null(Field(section).GetAttribute("aria-invalid"));
        Assert.Equal(port, _settings.Settings.Server.Port);
    }

    // Said when it's typed, not at the switch or the next start, when it would be harder to put right.
    [Fact]
    public void TypingAPortAnotherProgramHolds_SaysSoAndSavesNothing()
    {
        using var holder = new TcpListener(IPAddress.Loopback, 0);
        holder.Start();
        var port = ((IPEndPoint)holder.LocalEndpoint).Port;
        var section = RenderSection();

        Field(section).Change(port.ToString());

        Assert.Equal($"Port {port} is in use by another program.", Text(section, ".problem"));
        Assert.Null(_settings.Settings.Server.Port);
        Assert.Empty(section.FindAll(".notice"));
    }

    // It's the server's own port: something is holding it, and it's this.
    [Fact]
    public void TypingThePortInUse_NeedsNoCheck()
    {
        var port = FreePort();
        Choose(FreePort());
        var section = RenderSection(listening: new Uri($"http://localhost:{port}"));

        Field(section).Change(port.ToString());

        Assert.Equal(port, _settings.Settings.Server.Port);
        Assert.Empty(section.FindAll(".problem"));
        Assert.Empty(section.FindAll(".notice"));
    }

    // Choosing the default is choosing none, so a copy that never picked a port follows the default.
    [Fact]
    public void Undo_ToTheDefaultPortInUse_ChoosesNone()
    {
        Choose(FreePort());
        var section = RenderSection();

        section.Find(".notice .button-secondary").Click();

        Assert.Null(_settings.Settings.Server.Port);
        Assert.Empty(section.FindAll(".notice"));
        Assert.Equal("5062", Field(section).GetAttribute("value"));
    }

    [Fact]
    public void Undo_WhenAnotherPortIsInUse_ChoosesThatOne()
    {
        var running = FreePort();
        var other = FreePort();
        Choose(running);
        var section = RenderSection(listening: new Uri($"http://localhost:{running}"));
        Field(section).Change(other.ToString());

        section.Find(".notice .button-secondary").Click();

        Assert.Equal(running, _settings.Settings.Server.Port);
        Assert.Empty(section.FindAll(".notice"));
    }

    [Fact]
    public void SwitchNow_MovesTheServerToTheChosenPort()
    {
        var port = FreePort();
        var section = RenderSection();
        Field(section).Change(port.ToString());
        _answers = asked => Task.FromResult(asked == _network.ListenUrls[0].Port);

        section.Find(".notice .switch").Click();

        section.WaitForAssertion(() => Assert.Equal(port, _network.ListenUrls[0].Port));
        section.WaitForAssertion(() => Assert.Empty(section.FindAll(".notice"))); // On it: nothing left to switch to
        Assert.Equal(port, _settings.Settings.Server.Port);
        Assert.Empty(section.FindAll(".problem"));
    }

    [Fact]
    public void WhileItSwitches_SaysSoAndTheFieldCantBeChanged()
    {
        var port = FreePort();
        var release = new TaskCompletionSource<bool>();
        var section = RenderSection();
        Field(section).Change(port.ToString());
        _answers = asked => asked == port ? release.Task : Task.FromResult(false);

        section.Find(".notice .switch").Click();

        section.WaitForAssertion(() =>
        {
            Assert.Equal($"Switching to port {port}…", Text(section, ".notice b"));
            Assert.True(Field(section).HasAttribute("disabled"));
            Assert.Empty(section.FindAll(".notice button")); // Nothing to press meanwhile
        });
        release.SetResult(true);
        section.WaitForAssertion(() => Assert.Empty(section.FindAll(".notice")));
    }

    [Fact]
    public void ASwitchThatDidntWork_SaysWhy_AndTheServerIsBackWhereItWas()
    {
        var port = FreePort();
        var section = RenderSection();
        Field(section).Change(port.ToString());
        _answers = asked => Task.FromResult(asked == 5062 && _network.ListenUrls[0].Port == 5062); // The new port never answers

        section.Find(".notice .switch").Click();

        section.WaitForAssertion(() => Assert.Equal($"Couldn't start on port {port}, so Riftcaster is back on port 5062.", Text(section, ".problem")));
        Assert.Equal(5062, _network.ListenUrls[0].Port);
        Assert.Equal("5062", Field(section).GetAttribute("value"));
        Assert.Null(_settings.Settings.Server.Port); // The choice is the port that works
        Assert.Empty(section.FindAll(".notice"));
        Assert.False(Field(section).HasAttribute("disabled"));
    }

    [Fact]
    public void StartedWithUrls_ThePortCantBeChanged()
    {
        Choose(5070);
        var section = RenderSection(source: PortSource.LaunchOption, listening: new Uri("http://localhost:5099"));

        Assert.True(Field(section).HasAttribute("disabled"));
        Assert.Equal("5099", Field(section).GetAttribute("value")); // The one in use
        Assert.Equal("This run was started with --urls, so the saved port (5070) is ignored. Remove the option to use it.", Text(section, ".explanation"));
        Assert.Empty(section.FindAll(".notice")); // Whatever is saved, nothing can be switched to
    }

    // Nothing was saved, so there is no saved port to say is ignored: the default isn't one.
    [Fact]
    public void StartedWithUrlsAndNoSavedPort_DoesntMentionASavedPort()
    {
        var section = RenderSection(source: PortSource.LaunchOption, listening: new Uri("http://localhost:5062"));

        var text = Text(section, ".explanation");

        Assert.Equal("This run was started with --urls, which sets the port. Remove the option to choose one here.", text);
        Assert.DoesNotContain("saved port", text);
        Assert.DoesNotContain("5062", text);
    }

    // A developer's setup: only https addresses, so there's no HTTP port to show or set. Riftcaster doesn't
    // show or offer an HTTPS one, since an installed copy has no certificate to serve it with.
    [Fact]
    public void StartedWithOnlyHttpsAddresses_ThereIsNoPortToShow()
    {
        var section = RenderSection(source: PortSource.LaunchOption, listening: new Uri("https://localhost:7216"));

        Assert.Equal("", Field(section).GetAttribute("value"));
        Assert.True(Field(section).HasAttribute("disabled"));
        Assert.Contains("only https addresses", Text(section, ".explanation"));
        Assert.Empty(section.FindAll(".notice"));
    }

    // Only the web interface's port is ever shown: nothing about HTTPS, however the server was started.
    [Theory]
    [InlineData(PortSource.Default)]
    [InlineData(PortSource.Saved)]
    [InlineData(PortSource.LaunchOption)]
    public void NothingAboutHttpsIsShown(PortSource source)
    {
        var section = RenderSection(source: source);

        Assert.DoesNotContain("HTTPS", section.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(section.FindAll("input[aria-label*='HTTPS' i]"));
        Assert.Empty(section.FindAll(".bubble"));
    }

    // Another device can't change it either way, so that's what it's told, not what the launch option says.
    [Fact]
    public void FromAnotherDeviceWhenStartedWithUrls_SaysItCantChangeItFromThere()
    {
        var section = RenderSection(source: PortSource.LaunchOption, listening: new Uri("http://localhost:5099"), local: false);

        Assert.Equal("Only the computer running Riftcaster can change the port.", Text(section, ".explanation"));
    }

    [Fact]
    public void FromAnotherDevice_ThePortCantBeChanged()
    {
        Choose(FreePort());
        var section = RenderSection(local: false);

        Assert.True(Field(section).HasAttribute("disabled"));
        Assert.Equal("Only the computer running Riftcaster can change the port.", Text(section, ".explanation"));
        // It can still be told a switch is waiting, but has nothing to press.
        Assert.Contains("Switch Riftcaster to port", section.Find(".notice b").TextContent);
        Assert.Empty(section.FindAll(".notice button"));
    }

    [Fact]
    public void FollowsChangesMadeElsewhere()
    {
        var port = FreePort();
        var section = RenderSection();

        Choose(port);

        section.WaitForAssertion(() => Assert.Equal(port.ToString(), Field(section).GetAttribute("value")));
        Assert.Contains(port.ToString(), section.Find(".notice b").TextContent);
    }

    // Moved by someone else, such as the operator at the server: nothing is waiting any more.
    [Fact]
    public void FollowsAServerThatMovedElsewhere()
    {
        var port = FreePort();
        var section = RenderSection();
        Choose(port);
        section.WaitForAssertion(() => Assert.NotEmpty(section.FindAll(".notice")));

        _network.SetPort(port);
        _settings.Update(settings => settings with { Logging = new LoggingSettings { Detail = LogDetail.Detailed } }); // Anything that makes it look again

        section.WaitForAssertion(() => Assert.Empty(section.FindAll(".notice")));
    }

    [Fact]
    public void StopsListeningWhenRemoved()
    {
        var section = RenderSection();

        section.Dispose();

        // Would throw if the removed section still tried to render.
        Choose(FreePort());
    }
}
