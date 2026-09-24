using Riftcaster.Contracts;
using Riftcaster.Core.LowerThird;

namespace Riftcaster.Core.Tests;

public class LowerThirdServiceTests
{
    private readonly LowerThirdService _service = new();

    private int _changedCount;

    public LowerThirdServiceTests()
    {
        _service.Changed += () => _changedCount++;
    }

    [Fact]
    public void CurrentMessage_IsNullInitially()
    {
        Assert.Null(_service.CurrentMessage);
    }

    [Fact]
    public void Show_RaisesChanged()
    {
        _service.Show(new LowerThirdMessage("Burn", "Send cards to the trash."));
        Assert.Equal(1, _changedCount);
    }

    [Fact]
    public void Show_SetsCurrent()
    {
        var message = new LowerThirdMessage("Burn", "Send cards to the trash.");
        _service.Show(message);
        Assert.Equal(message, _service.CurrentMessage);
    }

    [Fact]
    public void Show_SameMessageAgain_DoesNotRaiseChanged()
    {
        string keyword = "Burn";
        string description = "Send cards to the trash.";
        var message1 = new LowerThirdMessage(keyword, description);
        var message2 = new LowerThirdMessage(keyword, description);
        Assert.NotSame(message1, message2);
        _service.Show(message1);
        _service.Show(message2);
        Assert.Equal(1, _changedCount);
    }

    [Fact]
    public void Show_DifferentMessage_RaisesChangedAgain()
    {
        var message1 = new LowerThirdMessage("Burn", "Send cards to the trash.");
        var message2 = new LowerThirdMessage("Draw", "Draw a card from your deck.");
        _service.Show(message1);
        _service.Show(message2);
        Assert.Equal(2, _changedCount);
        Assert.Equal(message2, _service.CurrentMessage);
    }

    [Fact]
    public void Hide_ClearsCurrentAndRaisesChanged()
    {
        var message = new LowerThirdMessage("Burn", "Send cards to the trash.");
        _service.Show(message);
        _service.Hide();
        Assert.Null(_service.CurrentMessage);
        Assert.Equal(2, _changedCount);
    }

    [Fact]
    public void Hide_WhenNothingShowing_DoesNotRaiseChanged()
    {
        _service.Hide();
        Assert.Equal(0, _changedCount);
    }
}
