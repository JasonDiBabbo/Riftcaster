using Riftcaster.Contracts;
using Riftcaster.Core.LowerThird;

namespace Riftcaster.Core.Tests;

public class LowerThirdServiceTests
{
    private readonly InMemoryLowerThirdStore _store = new();

    private readonly LowerThirdService _service;

    private int _changedCount;

    public LowerThirdServiceTests()
    {
        _service = new LowerThirdService(_store);
        _service.Changed += () => _changedCount++;
    }

    [Fact]
    public void Constructor_LoadsEntriesButNothingIsLive()
    {
        var message = new LowerThirdKeywordMessage("Burn", "Send cards to the trash.");
        var entry = new LowerThirdEntry(Guid.NewGuid(), message);
        _store.Save([entry]);

        var service = new LowerThirdService(_store);

        Assert.Equal([entry], service.Library.Entries);
        Assert.Null(service.CurrentMessage);
    }

    [Fact]
    public void Changes_AreSaved()
    {
        AddAndShow(new LowerThirdKeywordMessage("Burn", "Send cards to the trash."));

        Assert.Same(_service.Library.Entries, _store.Entries);
    }

    [Fact]
    public void ShowAndHide_DoNotSave()
    {
        var entry = _service.Add(new LowerThirdKeywordMessage("Burn", "Send cards to the trash."));
        var saveCount = _store.SaveCount;

        _service.Show(entry.Id);
        _service.Hide();

        Assert.Equal(saveCount, _store.SaveCount);
    }

    [Fact]
    public void Add_PrependsEntry()
    {
        var message1 = new LowerThirdKeywordMessage("Burn", "Send cards to the trash.");
        var message2 = new LowerThirdKeywordMessage("Draw", "Draw a card from your deck.");

        var entry1 = _service.Add(message1);
        var entry2 = _service.Add(message2);

        Assert.Equal([entry2, entry1], _service.Library.Entries);
    }

    [Fact]
    public void Add_RaisesChanged()
    {
        var message = new LowerThirdKeywordMessage("Burn", "Send cards to the trash.");

        _service.Add(message);

        Assert.Equal(1, _changedCount);
    }

    [Fact]
    public void Add_DoesNotShowEntry()
    {
        var message = new LowerThirdKeywordMessage("Burn", "Send cards to the trash.");

        _service.Add(message);

        Assert.Null(_service.CurrentMessage);
    }

    [Fact]
    public void CurrentMessage_IsNullInitially()
    {
        Assert.Null(_service.CurrentMessage);
    }

    [Fact]
    public void Delete_RemovesEntry()
    {
        var message = new LowerThirdKeywordMessage("Burn", "Send cards to the trash.");

        var newEntry = _service.Add(message);

        var deleted = _service.Delete(newEntry.Id);

        Assert.True(deleted);
        Assert.DoesNotContain(_service.Library.Entries, (entry) => entry.Id == newEntry.Id);
    }

    [Fact]
    public void Delete_LiveEntry_ClearsCurrentMessage()
    {
        var message = new LowerThirdKeywordMessage("Burn", "Send cards to the trash.");
        var entry = AddAndShow(message);

        var deleted = _service.Delete(entry.Id);

        Assert.True(deleted);
        Assert.Null(_service.Library.LiveEntryId);
        Assert.Null(_service.CurrentMessage);
    }

    [Fact]
    public void Delete_OtherEntry_KeepsLive()
    {
        var message1 = new LowerThirdKeywordMessage("Burn", "Send cards to the trash.");
        var message2 = new LowerThirdKeywordMessage("Draw", "Draw a card from your deck.");

        var entry1 = AddAndShow(message1);
        var entry2 = _service.Add(message2);

        var delete2 = _service.Delete(entry2.Id);

        Assert.True(delete2);
        Assert.Equal(message1, _service.CurrentMessage);
        Assert.Equal(entry1.Id, _service.Library.LiveEntryId);
    }

    [Fact]
    public void Delete_UnknownId_ReturnsFalse()
    {
        var deleted = _service.Delete(Guid.NewGuid());

        Assert.False(deleted);
        Assert.Equal(0, _changedCount);
    }

    [Fact]
    public void Library_IsEmptyInitially()
    {
        Assert.Empty(_service.Library.Entries);
    }

    [Fact]
    public void Show_RaisesChanged()
    {
        var message = new LowerThirdKeywordMessage("Burn", "Send cards to the trash.");
        var entry = _service.Add(message);
        _changedCount = 0;

        _service.Show(entry.Id);

        Assert.Equal(1, _changedCount);
    }

    [Fact]
    public void Show_SetsCurrent()
    {
        var message = new LowerThirdKeywordMessage("Burn", "Send cards to the trash.");
        var entry = _service.Add(message);

        _service.Show(entry.Id);

        Assert.Equal(message, _service.CurrentMessage);
        Assert.Equal(entry.Id, _service.Library.LiveEntryId);
    }

    [Fact]
    public void Show_SameEntryAgain_DoesNotRaiseChanged()
    {
        var message = new LowerThirdKeywordMessage("Burn", "Send cards to the trash.");
        var entry = AddAndShow(message);
        _changedCount = 0;

        _service.Show(entry.Id);

        Assert.Equal(0, _changedCount);
    }

    [Fact]
    public void Show_DifferentEntry_RaisesChangedAgain()
    {
        var message1 = new LowerThirdKeywordMessage("Burn", "Send cards to the trash.");
        var message2 = new LowerThirdKeywordMessage("Draw", "Draw a card from your deck.");

        var entry1 = _service.Add(message1);
        var entry2 = _service.Add(message2);
        _changedCount = 0;

        _service.Show(entry1.Id);
        _service.Show(entry2.Id);

        Assert.Equal(2, _changedCount);
        Assert.Equal(message2, _service.CurrentMessage);
        Assert.Equal(entry2.Id, _service.Library.LiveEntryId);
    }

    [Fact]
    public void Show_UnknownId_ReturnsFalse()
    {
        bool showed = _service.Show(Guid.NewGuid());

        Assert.False(showed);
        Assert.Equal(0, _changedCount);
    }

    [Fact]
    public void Hide_ClearsCurrentAndRaisesChanged()
    {
        var message = new LowerThirdKeywordMessage("Burn", "Send cards to the trash.");
        AddAndShow(message);
        _changedCount = 0;

        _service.Hide();

        Assert.Null(_service.CurrentMessage);
        Assert.Equal(1, _changedCount);
    }

    [Fact]
    public void Hide_WhenNothingShowing_DoesNotRaiseChanged()
    {
        Assert.Null(_service.CurrentMessage);

        _service.Hide();

        Assert.Equal(0, _changedCount);
    }

    [Fact]
    public void Update_ChangesEntryMessage()
    {
        var message = new LowerThirdKeywordMessage("Burn", "Send cards to the trash.");
        var updatedMessage = new LowerThirdKeywordMessage("Draw", "Draw a card from your deck.");

        var entry = _service.Add(message);

        var updated = _service.Update(entry.Id, updatedMessage);

        Assert.True(updated);
        var stored = Assert.Single(_service.Library.Entries);
        Assert.Equal(entry.Id, stored.Id);
        Assert.Equal(updatedMessage, stored.Message);
    }

    [Fact]
    public void Update_LiveEntry_ChangesCurrentMessage()
    {
        var message = new LowerThirdKeywordMessage("Burn", "Send cards to the trash.");
        var updatedMessage = new LowerThirdKeywordMessage("Draw", "Draw a card from your deck.");

        var entry = AddAndShow(message);

        var update = _service.Update(entry.Id, updatedMessage);

        Assert.True(update);
        Assert.Equal(updatedMessage, _service.CurrentMessage);
    }

    [Fact]
    public void Update_SameContent_DoesNotRaiseChanged()
    {
        var message1 = new LowerThirdKeywordMessage("Burn", "Send cards to the trash.");
        var message2 = new LowerThirdKeywordMessage("Burn", "Send cards to the trash.");
        Assert.NotSame(message1, message2);

        var entry = _service.Add(message1);
        _changedCount = 0;

        bool updated = _service.Update(entry.Id, message2);

        Assert.True(updated);
        Assert.Equal(0, _changedCount);
    }

    [Fact]
    public void Update_SameValuesDifferentType_RaisesChanged()
    {
        var keywordMessage = new LowerThirdKeywordMessage("Burn", "Send cards to the trash.");
        var otherMessage = new OtherMessage("Burn", "Send cards to the trash.");

        var entry = _service.Add(keywordMessage);
        _changedCount = 0;

        _service.Update(entry.Id, otherMessage);

        Assert.Equal(1, _changedCount);
    }

    [Fact]
    public void Update_UnknownId_ReturnsFalse()
    {
        var message = new LowerThirdKeywordMessage("Burn", "Send cards to the trash.");

        bool updated = _service.Update(Guid.NewGuid(), message);

        Assert.False(updated);
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
        AddAndShow(message);

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

        AddAndShow(message1);
        AddAndShow(message2);

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
    public async Task WatchAsync_IgnoresChangesThatDoNotAffectLiveMessage()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var watch = _service.WatchAsync(cts.Token).GetAsyncEnumerator();
        Assert.True(await watch.MoveNextAsync()); // Consume the initial state
        var next = watch.MoveNextAsync(); // Wait for the next event before changing anything

        var message = new LowerThirdKeywordMessage("Burn", "Send cards to the trash.");
        _service.Add(new LowerThirdKeywordMessage("Draw", "Draw a card from your deck.")); // Not live: no event
        AddAndShow(message);

        Assert.True(await next);
        Assert.Equal(message, watch.Current.Message); // The live change, not an unchanged null
    }

    private LowerThirdEntry AddAndShow(LowerThirdMessage message)
    {
        var entry = _service.Add(message);
        _service.Show(entry.Id);
        return entry;
    }

    /// <summary>
    /// A distinct message type for testing with the same shape as <see cref="LowerThirdKeywordMessage"/>.
    /// </summary>
    /// <param name="Keyword">The message keyword</param>
    /// <param name="Description">The message description</param>
    private sealed record OtherMessage(string Keyword, string Description) : LowerThirdMessage;
}
