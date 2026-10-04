using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Riftcaster.Admin.Layout;
using Riftcaster.Core.Network;

namespace Riftcaster.Admin.Tests;

public class RemoteSessionGuardTests : BunitContext
{
    private readonly AccessCode _accessCode = new(new InMemoryAccessCodeStore());

    public RemoteSessionGuardTests()
    {
        Services.AddSingleton(_accessCode);
        _accessCode.Set("K7QM-4XPA");
    }

    private NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    [Fact]
    public void RemotePage_CodeChanged_IsSentToSignIn()
    {
        var guard = RenderGuard(isLocal: false);
        Assert.Equal("http://localhost/", Navigation.Uri);

        _accessCode.Set("NEWC-ODE7");

        guard.WaitForAssertion(() => Assert.Equal("http://localhost/signin", Navigation.Uri));
    }

    [Fact]
    public void RemotePage_CodeRemoved_IsSentToSignIn()
    {
        var guard = RenderGuard(isLocal: false);

        _accessCode.Remove();

        guard.WaitForAssertion(() => Assert.Equal("http://localhost/signin", Navigation.Uri));
    }

    [Fact]
    public void RemotePage_CodeChangedBeforeItConnected_IsSentToSignIn()
    {
        // The page was opened with an older code than the current one.
        Render<RemoteSessionGuard>(parameters => parameters.AddCascadingValue(new OperatorSession(IsLocal: false, CodeVersion: "older")));

        Assert.Equal("http://localhost/signin", Navigation.Uri);
    }

    [Fact]
    public void LocalPage_CodeChanged_StaysPut()
    {
        RenderGuard(isLocal: true);

        _accessCode.Set("NEWC-ODE7");

        Assert.Equal("http://localhost/", Navigation.Uri);
    }

    [Fact]
    public void RemotePage_CodeUnchanged_StaysPut()
    {
        RenderGuard(isLocal: false);

        Assert.Equal("http://localhost/", Navigation.Uri);
    }

    private IRenderedComponent<RemoteSessionGuard> RenderGuard(bool isLocal) =>
        Render<RemoteSessionGuard>(parameters => parameters.AddCascadingValue(new OperatorSession(isLocal, _accessCode.Version)));
}
