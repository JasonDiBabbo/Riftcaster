using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Riftcaster.Admin.Layout;
using Riftcaster.Core.Network;

namespace Riftcaster.Admin.Tests;

public class NetworkControlTests : BunitContext
{
    private readonly NetworkAccess _network = new(false, [NetworkAccess.DefaultUrl]);

    private readonly AccessCode _accessCode = new(new InMemoryAccessCodeStore());

    public NetworkControlTests()
    {
        Services.AddSingleton(_network);
        Services.AddSingleton(_accessCode);
    }

    [Fact]
    public void Pill_SaysWhetherNetworkAccessIsOn()
    {
        var control = RenderControl(isLocal: true);
        Assert.Equal("Local only", control.Find(".pill").TextContent.Trim());

        _network.SetEnabled(true);

        control.WaitForAssertion(() => Assert.Equal("On network", control.Find(".pill").TextContent.Trim()));
    }

    [Fact]
    public void ThisComputer_CanSwitchNetworkAccess()
    {
        var control = RenderOpen(isLocal: true);

        control.Find("#network-panel .toggle").Click();

        Assert.True(_network.Enabled);
    }

    [Fact]
    public void ThisComputer_CanSetAGeneratedCode()
    {
        var control = RenderOpen(isLocal: true);

        Button(control, "Generate").Click();
        var generated = control.Find(".code-form input").GetAttribute("value");
        Button(control, "Set").Click();

        Assert.Equal(generated, _accessCode.Code);
        Assert.Equal(generated, control.Find(".code").TextContent); // Shown straight away, to read out
    }

    [Fact]
    public void ThisComputer_TooShortACode_IsRefused()
    {
        var control = RenderOpen(isLocal: true);

        control.Find(".code-form input").Input("12345");
        Button(control, "Set").Click();

        Assert.False(_accessCode.IsSet);
        Assert.Contains("Use 6 to 64 characters", control.Find("[role=alert]").TextContent);
    }

    [Fact]
    public void NetworkOn_SaysHowToGetThroughAFirewall()
    {
        _network.SetEnabled(true);

        var control = RenderOpen(isLocal: true);

        Assert.Contains("allow the server through this computer's firewall: it may be listed as dotnet, or allow port 5062.",
            control.Find("#network-panel").TextContent);
        Assert.DoesNotContain("Windows", control.Find("#network-panel").TextContent);
    }

    [Fact]
    public void NetworkOn_SeveralAddresses_NamesEachPort()
    {
        var network = new NetworkAccess(true, [new Uri("http://localhost:5062"), new Uri("https://localhost:7216"), new Uri("http://127.0.0.1:5062")]);
        Services.AddSingleton(network); // Replaces the class's own, before the first render

        var control = RenderOpen(isLocal: true);

        Assert.Contains("or allow ports 5062 and 7216.", control.Find("#network-panel").TextContent);
    }

    [Fact]
    public void ThisComputer_CodeIsHiddenUntilShown()
    {
        _accessCode.Set("K7QM-4XPA");
        var control = RenderOpen(isLocal: true);
        Assert.DoesNotContain("K7QM", control.Find(".code").TextContent);

        Button(control, "Show").Click();

        Assert.Equal("K7QM-4XPA", control.Find(".code").TextContent);
    }

    [Fact]
    public void ThisComputer_CanRemoveTheCode()
    {
        _accessCode.Set("K7QM-4XPA");
        var control = RenderOpen(isLocal: true);

        Button(control, "Remove code").Click();

        Assert.False(_accessCode.IsSet);
    }

    [Fact]
    public void RemoteOperator_CannotSwitchNetworkAccessOrChangeTheCode()
    {
        _accessCode.Set("K7QM-4XPA");
        _network.SetEnabled(true);

        var control = RenderOpen(isLocal: false);

        Assert.Empty(control.FindAll("#network-panel .toggle"));
        Assert.Empty(control.FindAll(".code-form"));
        Assert.DoesNotContain("K7QM", control.Markup); // Not even hidden in the page
        Assert.Equal("/signout", control.Find("form").GetAttribute("action")); // But can sign out
    }

    [Fact]
    public void Escape_ClosesThePanel()
    {
        var control = RenderOpen(isLocal: true);

        control.Find("#network-panel").KeyDown("Escape");

        Assert.Empty(control.FindAll("#network-panel"));
    }

    [Fact]
    public void ClickingOutside_ClosesThePanel()
    {
        var control = RenderOpen(isLocal: true);

        control.Find(".scrim").Click();

        Assert.Empty(control.FindAll("#network-panel"));
    }

    private IRenderedComponent<NetworkControl> RenderControl(bool isLocal) =>
        Render<NetworkControl>(parameters => parameters.AddCascadingValue(new OperatorSession(isLocal, _accessCode.Version)));

    private IRenderedComponent<NetworkControl> RenderOpen(bool isLocal)
    {
        var control = RenderControl(isLocal);
        control.Find(".pill").Click();
        return control;
    }

    private static AngleSharp.Dom.IElement Button(IRenderedComponent<NetworkControl> control, string text) =>
        control.FindAll("#network-panel button").Single(button => button.TextContent.Trim() == text);
}
