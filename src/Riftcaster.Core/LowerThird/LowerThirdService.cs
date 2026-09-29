using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Riftcaster.Contracts;

namespace Riftcaster.Core.LowerThird;

public class LowerThirdService
{
    private readonly Lock _lock = new();

    private LowerThirdLibrary _library = LowerThirdLibrary.Empty;

    public LowerThirdLibrary Library => _library;

    public LowerThirdMessage? CurrentMessage => _library.LiveMessage;

    public event Action? Changed;

    public LowerThirdEntry Add(LowerThirdMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        LowerThirdEntry entry = new(Guid.NewGuid(), message);

        lock (_lock)
        {
            _library = _library with { Entries = _library.Entries.Insert(0, entry) };
        }

        Changed?.Invoke();
        return entry;
    }

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

            _library = _library with { LiveEntryId = id };
        }

        Changed?.Invoke();
        return true;
    }

    public void Hide()
    {
        lock (_lock)
        {
            if (_library.LiveEntryId is null)
            {
                return;
            }

            _library = _library with { LiveEntryId = null };
        }

        Changed?.Invoke();
    }

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

            _library = _library with { Entries = _library.Entries.SetItem(index, existingEntry with { Message = message }) };
        }

        Changed?.Invoke();
        return true;
    }

    public bool Delete(Guid id)
    {
        lock (_lock)
        {
            var index = _library.Entries.FindIndex((entry) => entry.Id == id);
            if (index == -1)
            {
                return false;
            }

            _library = new LowerThirdLibrary(
                _library.Entries.RemoveAt(index),
                _library.LiveEntryId == id ? null : _library.LiveEntryId);
        }

        Changed?.Invoke();
        return true;
    }

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
}
