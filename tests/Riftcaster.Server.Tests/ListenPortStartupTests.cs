using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Riftcaster.Core.Network;

namespace Riftcaster.Server.Tests;

// Which port the server listens on at its start: --urls if it was given one, else the one chosen on the Settings
// page, else the default. The test server doesn't listen, but works the address out the same way.
public class ListenPortStartupTests
{
    private sealed class PortFactory : RiftcasterWebApplicationFactory
    {
        private readonly string? _urls;

        public PortFactory(string? savedSettings = null, string? urls = null)
        {
            _urls = urls;
            if (savedSettings is not null)
            {
                File.WriteAllText(Path.Combine(DataDirectory, "settings.json"), savedSettings);
            }
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            if (_urls is not null)
            {
                builder.UseSetting("urls", _urls);
            }
        }
    }

    private static (PortSource Source, int? HttpPort, int ListeningOn) Start(string? savedSettings = null, string? urls = null)
    {
        using var factory = new PortFactory(savedSettings, urls);
        var origin = factory.Services.GetRequiredService<PortOrigin>();
        var network = factory.Services.GetRequiredService<NetworkAccess>();
        return (origin.Source, ListenPort.HttpPort(network.ListenUrls), network.ListenUrls[0].Port);
    }

    [Fact]
    public void WithNothingChosen_ListensOnTheDefault()
    {
        var (source, http, listeningOn) = Start();

        Assert.Equal(PortSource.Default, source);
        Assert.Equal(ListenPort.Default, http);
        Assert.Equal(ListenPort.Default, listeningOn);
    }

    [Fact]
    public void WithAPortChosen_ListensOnIt()
    {
        var (source, http, listeningOn) = Start("""{"server":{"port":5070}}""");

        Assert.Equal(PortSource.Saved, source);
        Assert.Equal(5070, http);
        Assert.Equal(5070, listeningOn);
    }

    [Fact]
    public void WithALaunchOption_ListensOnItAndIgnoresTheChosenPort()
    {
        var (source, http, listeningOn) = Start("""{"server":{"port":5070}}""", urls: "http://localhost:5099");

        Assert.Equal(PortSource.LaunchOption, source);
        Assert.Equal(5099, http);
        Assert.Equal(5099, listeningOn);
    }

    // Visual Studio's https profile gives an https and an http address: the web interface's port is the http one.
    [Fact]
    public void WithAnHttpsAndAnHttpAddress_TheHttpPortIsTheHttpOne()
    {
        var (_, http, listeningOn) = Start(urls: "https://localhost:5099;http://localhost:5098");

        Assert.Equal(5098, http);
        Assert.Equal(5099, listeningOn); // The first address, which the launcher uses to open the web interface
    }

    [Fact]
    public void WithOnlyAnHttpsAddress_HasNoHttpPort()
    {
        var (_, http, _) = Start(urls: "https://localhost:5099");

        Assert.Null(http);
    }

    [Fact]
    public void WithALaunchOptionAndNothingChosen_ItIsTheLaunchOption()
    {
        var (source, _, _) = Start(urls: "http://localhost:5099");

        Assert.Equal(PortSource.LaunchOption, source);
    }

    // A file the server can't use as it is (an unusable port, or no valid JSON) is none: the default.
    [Theory]
    [InlineData("""{"server":{"port":80}}""")]
    [InlineData("""{"server":{"port":"5070"}}""")]
    [InlineData("not json")]
    public void WithAPortItCantUse_ListensOnTheDefault(string savedSettings)
    {
        var (source, http, listeningOn) = Start(savedSettings);

        Assert.Equal(PortSource.Default, source);
        Assert.Equal(ListenPort.Default, http);
        Assert.Equal(ListenPort.Default, listeningOn);
    }

    [Fact]
    public void TheServerCanMoveItself()
    {
        using var factory = new PortFactory();

        Assert.NotNull(factory.Services.GetRequiredService<PortSwitcher>());
        Assert.Equal(ListenPort.Default, factory.Services.GetRequiredService<PortSwitcher>().RunningPort);
    }
}
