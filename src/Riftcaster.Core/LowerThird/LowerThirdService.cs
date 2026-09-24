using Riftcaster.Contracts;

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
}
