using Microsoft.AspNetCore.Connections;
using Riftcaster.Core.Network;

namespace Riftcaster.Server.Tests;

// Telling that Riftcaster couldn't start because its port is taken, and what the console server says about it.
public class PortConflictTests
{
    [Fact]
    public void IsPortInUse_ForTheExceptionKestrelThrows()
    {
        Assert.True(PortConflict.IsPortInUse(new AddressInUseException("Address already in use")));
    }

    // Starting wraps it: the host reports the server's failure inside its own.
    [Fact]
    public void IsPortInUse_ForOneInsideAnother()
    {
        var wrapped = new IOException("Failed to bind to address http://127.0.0.1:5062", new AddressInUseException("in use"));

        Assert.True(PortConflict.IsPortInUse(new InvalidOperationException("Host failed", wrapped)));
    }

    [Fact]
    public void IsPortInUse_IsFalseForAnythingElse()
    {
        Assert.False(PortConflict.IsPortInUse(new IOException("Failed to bind", new UnauthorizedAccessException())));
        Assert.False(PortConflict.IsPortInUse(new InvalidOperationException("Something else")));
    }

    [Fact]
    public void ConsoleMessage_SaysWhichPortAndSuggestsAnotherWithUrls()
    {
        var message = PortConflict.ConsoleMessage(5062, PortSource.Default, 5063);

        Assert.Contains("Port 5062 is already in use by another program", message);
        Assert.Contains("--urls http://localhost:5063", message);
        Assert.Contains("Is Riftcaster already running?", message);
    }

    [Fact]
    public void ConsoleMessage_ForAPortFromSettings_SaysSo()
    {
        var message = PortConflict.ConsoleMessage(5070, PortSource.Saved, 5071);

        Assert.Contains("Port 5070, the one chosen in Settings", message);
        Assert.DoesNotContain("already running", message);
    }

    [Fact]
    public void ConsoleMessage_ForAPortFromUrls_SaysSo()
    {
        var message = PortConflict.ConsoleMessage(5099, PortSource.LaunchOption, 5100);

        Assert.Contains("Port 5099, from the --urls option", message);
    }

    [Fact]
    public void ConsoleMessage_WithNoFreePortFound_StillSaysHowToChooseOne()
    {
        var message = PortConflict.ConsoleMessage(5062, PortSource.Default, suggestion: null);

        Assert.Contains("--urls http://localhost:<port>", message);
    }
}
