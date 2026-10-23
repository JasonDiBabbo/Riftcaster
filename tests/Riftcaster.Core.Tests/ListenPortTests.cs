using Riftcaster.Core.Network;

namespace Riftcaster.Core.Tests;

public class ListenPortTests
{
    [Fact]
    public void Default_IsThePortOfTheDefaultAddress()
    {
        Assert.Equal(5062, ListenPort.Default);
        Assert.Equal(NetworkAccess.DefaultUrl.Port, ListenPort.Default);
        Assert.True(ListenPort.IsValid(ListenPort.Default));
    }

    [Theory]
    [InlineData(ListenPort.Min, true)]
    [InlineData(ListenPort.Max, true)]
    [InlineData(5062, true)]
    [InlineData(ListenPort.Min - 1, false)] // The well-known ports
    [InlineData(80, false)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(ListenPort.Max + 1, false)]
    public void IsValid_IsTheRangeThatCanBeChosen(int port, bool valid)
    {
        Assert.Equal(valid, ListenPort.IsValid(port));
    }

    [Theory]
    [InlineData("5070", 5070)]
    [InlineData("  5070 ", 5070)]
    [InlineData("1024", 1024)]
    [InlineData("65535", 65535)]
    public void TryParse_ReadsAPortThatCanBeChosen(string text, int expected)
    {
        Assert.True(ListenPort.TryParse(text, out var port));
        Assert.Equal(expected, port);
    }

    [Fact]
    public void HttpPort_IsThePortOfTheFirstHttpAddress()
    {
        Assert.Equal(5062, ListenPort.HttpPort([new Uri("http://localhost:5062")]));
        Assert.Equal(5062, ListenPort.HttpPort([new Uri("https://localhost:7216"), new Uri("http://localhost:5062")])); // Not the first address
        Assert.Equal(5070, ListenPort.HttpPort([new Uri("http://localhost:5070"), new Uri("http://localhost:5071")]));
    }

    [Fact]
    public void HttpPort_WithOnlyHttpsAddresses_IsNone()
    {
        Assert.Null(ListenPort.HttpPort([new Uri("https://localhost:7216")]));
        Assert.Null(ListenPort.HttpPort([]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("abc")]
    [InlineData("50 70")]
    [InlineData("5070.5")]
    [InlineData("-5070")]
    [InlineData("+5070")]
    [InlineData("1023")]
    [InlineData("65536")]
    [InlineData("99999999999")]
    public void TryParse_RejectsAnythingElse(string? text)
    {
        Assert.False(ListenPort.TryParse(text, out _));
    }
}
