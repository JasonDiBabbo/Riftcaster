using Riftcaster.Contracts;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Riftcaster.Core.LowerThird;

public class LowerThirdService
{
    public LowerThirdMessage? CurrentMessage { get; private set; }

    public event Action? Changed;

    public void Show(LowerThirdMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message == CurrentMessage)
        {
            return;
        }

        CurrentMessage = message;
        Changed?.Invoke();
    }

    public void Hide()
    {
        if (CurrentMessage is null)
        {
            return;
        }

        CurrentMessage = null;
        Changed?.Invoke();
    }

    public async IAsyncEnumerable<LowerThirdState> WatchAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateBounded<LowerThirdState>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });

        void OnChanged()
        {
            channel.Writer.TryWrite(new LowerThirdState(CurrentMessage));
        }

        using var registration = cancellationToken.Register(() => channel.Writer.TryComplete());
        Changed += OnChanged;

        try
        {
            OnChanged(); // Initial state

            // Not cancellationToken: cancellation completes the channel (above), which ends this loop
            // normally. Passing the token would end it with an OperationCanceledException instead.
            await foreach (LowerThirdState state in channel.Reader.ReadAllAsync(CancellationToken.None))
            {
                yield return state;
            }
        }
        finally
        {
            Changed -= OnChanged;
        }
    }
}
