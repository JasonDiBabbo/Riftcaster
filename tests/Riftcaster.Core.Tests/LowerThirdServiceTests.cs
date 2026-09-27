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
        _service.Show(new LowerThirdKeywordMessage("Burn", "Send cards to the trash."));
        Assert.Equal(1, _changedCount);
    }

    [Fact]
    public void Show_SetsCurrent()
    {
        var message = new LowerThirdKeywordMessage("Burn", "Send cards to the trash.");
        _service.Show(message);
        Assert.Equal(message, _service.CurrentMessage);
    }

    [Fact]
    public void Show_SameMessageAgain_DoesNotRaiseChanged()
    {
        string keyword = "Burn";
        string description = "Send cards to the trash.";
        var message1 = new LowerThirdKeywordMessage(keyword, description);
        var message2 = new LowerThirdKeywordMessage(keyword, description);
        Assert.NotSame(message1, message2);
        _service.Show(message1);
        _service.Show(message2);
        Assert.Equal(1, _changedCount);
    }

    [Fact]
    public void Show_DifferentMessage_RaisesChangedAgain()
    {
        var message1 = new LowerThirdKeywordMessage("Burn", "Send cards to the trash.");
        var message2 = new LowerThirdKeywordMessage("Draw", "Draw a card from your deck.");
        _service.Show(message1);
        _service.Show(message2);
        Assert.Equal(2, _changedCount);
        Assert.Equal(message2, _service.CurrentMessage);
    }

    [Fact]
    public void Hide_ClearsCurrentAndRaisesChanged()
    {
        var message = new LowerThirdKeywordMessage("Burn", "Send cards to the trash.");
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

    [Fact]
    public async Task WatchAsync_YieldsCurrentStateFirst()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var watch = _service.WatchAsync(cts.Token).GetAsyncEnumerator();

        Assert.True(await watch.MoveNextAsync());
        Assert.Null(watch.Current.Message);
    }

    [Fact]
    public async Task WatchAsync_YieldsChanges()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var watch = _service.WatchAsync(cts.Token).GetAsyncEnumerator();
        Assert.True(await watch.MoveNextAsync()); // Consume the initial state

        var message = new LowerThirdKeywordMessage("Burn", "Send cards to the trash.");
        _service.Show(message);

        Assert.True(await watch.MoveNextAsync());
        Assert.Equal(message, watch.Current.Message);
    }

    [Fact]
    public async Task WatchAsync_WhenChangesPileUp_YieldsOnlyLatest()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var watch = _service.WatchAsync(cts.Token).GetAsyncEnumerator();
        Assert.True(await watch.MoveNextAsync()); // Consume the initial state

        var message1 = new LowerThirdKeywordMessage("Burn", "Send cards to the trash.");
        var message2 = new LowerThirdKeywordMessage("Draw", "Draw a card from your deck.");

        _service.Show(message1);
        _service.Show(message2);
        Assert.True(await watch.MoveNextAsync()); // Only the latest change
        Assert.Equal(message2, watch.Current.Message);
    }

    [Fact]
    public async Task WatchAsync_EndsWhenCancelled()
    {
        using var cts = new CancellationTokenSource(); // no timeout: we cancel it ourselves
        await using var watch = _service.WatchAsync(cts.Token).GetAsyncEnumerator();
        Assert.True(await watch.MoveNextAsync());

        cts.Cancel(); // Register callback -> channel

        Assert.False(await watch.MoveNextAsync()); // Stream ended normally
    }

    [Fact]
    public void Show_SameValuesDifferentType_RaisesChangedAgain()
    {
        var keywordMessage = new LowerThirdKeywordMessage("Burn", "Send cards to the trash.");
        var otherMessage = new OtherMessage("Burn", "Send cards to the trash.");

        _service.Show(keywordMessage);
        _service.Show(otherMessage);

        Assert.Equal(2, _changedCount);
    }

    /// <summary>
    /// A distinct message type for testing with the same shape as <see cref="LowerThirdKeywordMessage"/>.
    /// </summary>
    /// <param name="Keyword">The message keyword</param>
    /// <param name="Description">The message description</param>
    private sealed record OtherMessage(string Keyword, string Description) : LowerThirdMessage;
}
