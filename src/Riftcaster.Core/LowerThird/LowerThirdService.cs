using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Riftcaster.Contracts;

namespace Riftcaster.Core.LowerThird;

/// <summary>
/// The service for interacting with the lower third library (e.g. CRUD operations, persistence, and change notifications).
/// </summary>
/// <param name="store">Where the library is loaded from and saved to.</param>
public class LowerThirdService(ILowerThirdStore store)
{
    private readonly ILowerThirdStore _store = store;

    private readonly Lock _lock = new();

    private LowerThirdLibrary _library = new(store.Load(), LiveEntryId: null);

    /// <summary>
    /// The current library of saved lower third entries and which one is live.
    /// </summary>
    public LowerThirdLibrary Library => _library;

    /// <summary>
    /// The message on air, or <see langword="null"/> when nothing is showing.
    /// </summary>
    public LowerThirdMessage? CurrentMessage => _library.LiveMessage;

    /// <summary>
    /// Raised after any change to the library. Handlers read <see cref="Library"/> for the new state.
    /// </summary>
    /// <remarks>
    /// May be raised on any thread.
    /// </remarks>
    public event Action? Changed;

    /// <summary>
    /// Adds a new entry at the top of the library. It isn't shown until <see cref="Show"/> is called.
    /// </summary>
    /// <param name="message">The message to add to the library.</param>
    /// <returns>The new entry, including its generated id.</returns>
    public LowerThirdEntry Add(LowerThirdMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        LowerThirdEntry entry = new(Guid.NewGuid(), message);

        lock (_lock)
        {
            Set(_library with { Entries = _library.Entries.Insert(0, entry) });
        }

        Changed?.Invoke();
        return entry;
    }

    /// <summary>
    /// Puts an entry on air, replacing whatever is currently showing.
    /// </summary>
    /// <param name="id">The unique identifier of the entry to show.</param>
    /// <returns><see langword="true"/> if the entry is shown, and <see langword="false"/> if no entry has that id.</returns>
    public bool Show(Guid id)
    {
        lock (_lock)
        {
            if (!_library.Entries.Exists((entry) => entry.Id == id))
            {
                return false;
            }

            if (_library.LiveEntryId == id)
            {
                return true;
            }

            Set(_library with { LiveEntryId = id });
        }

        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Takes the live entry off air. Does nothing if nothing is showing.
    /// </summary>
    public void Hide()
    {
        lock (_lock)
        {
            if (_library.LiveEntryId is null)
            {
                return;
            }

            Set(_library with { LiveEntryId = null });
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Replaces an entry's message. If the entry is live, the overlay updates immediately.
    /// </summary>
    /// <param name="id">The id of the entry to update.</param>
    /// <param name="message">The new message to apply.</param>
    /// <returns><see langword="true"/> if the entry is updated, and <see langword="false"/> if no entry has that id.</returns>
    public bool Update(Guid id, LowerThirdMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        lock (_lock)
        {
            var index = _library.Entries.FindIndex((entry) => entry.Id == id);
            if (index == -1)
            {
                return false;
            }

            var existingEntry = _library.Entries[index];
            if (existingEntry.Message == message)
            {
                return true;
            }

            Set(_library with { Entries = _library.Entries.SetItem(index, existingEntry with { Message = message }) });
        }

        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Removes an entry. If it was live, the overlay clears.
    /// </summary>
    /// <param name="id">The id of the entry to remove.</param>
    /// <returns><see langword="true"/> if the entry is removed, and <see langword="false"/> if no entry has that id.</returns>
    public bool Delete(Guid id)
    {
        lock (_lock)
        {
            var index = _library.Entries.FindIndex((entry) => entry.Id == id);
            if (index == -1)
            {
                return false;
            }

            Set(new LowerThirdLibrary(
                _library.Entries.RemoveAt(index),
                _library.LiveEntryId == id ? null : _library.LiveEntryId));
        }

        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Streams what's on air: the current state first, then each change to it, until cancelled.
    /// Changes to entries that aren't live are skipped, and a burst of changes yields only the latest.
    /// </summary>
    /// <param name="cancellationToken">Ends the stream when cancelled, for example when the overlay disconnects.</param>
    /// <returns>The on-air state, first as it is now and then after each change.</returns>
    public async IAsyncEnumerable<LowerThirdState> WatchAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // A signal only: the reader looks up the current state itself, so it always sends the latest.
        var changed = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });

        void OnChanged()
        {
            changed.Writer.TryWrite(true);
        }

        using var registration = cancellationToken.Register(() => changed.Writer.TryComplete());
        Changed += OnChanged;

        try
        {
            var last = CurrentMessage;
            yield return new LowerThirdState(last); // Initial state

            // Not cancellationToken: cancellation completes the channel (above), which ends this loop
            // normally. Passing the token would end it with an OperationCanceledException instead.
            await foreach (var _ in changed.Reader.ReadAllAsync(CancellationToken.None))
            {
                var current = CurrentMessage;
                if (current == last)
                {
                    continue; // The library changed, but not what's on air (e.g. an entry was added)
                }

                last = current;
                yield return new LowerThirdState(current);
            }
        }
        finally
        {
            Changed -= OnChanged;
        }
    }

    /// <summary>
    /// Updates the library and stores the latest state.
    /// </summary>
    /// <remarks>
    /// Must be called inside the lock so that saves
    /// reach the store in the same order as the changes.
    /// Will only save when the library entries have changed.
    /// </remarks>
    /// <param name="library">The library to assign</param>
    private void Set(LowerThirdLibrary library)
    {
        var entriesChanged = !ReferenceEquals(library.Entries, _library.Entries);
        _library = library;

        if (entriesChanged)
        {
            _store.Save(library.Entries);
        }
    }
}
