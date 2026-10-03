using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Riftcaster.Contracts;
using Riftcaster.Core.Cards;

namespace Riftcaster.Core.FeaturedCard;

/// <summary>
/// The card on the Featured card overlay: choosing it, clearing it, and streaming it to the overlay.
/// </summary>
/// <remarks>
/// Keeps a copy of the card as it was when it was featured, not just its id, so it stays on air
/// across a restart even while the catalogue is still loading, or after a refresh changes it.
/// </remarks>
/// <param name="store">Where the featured card is loaded from and saved to.</param>
/// <param name="catalog">Where cards are looked up when featured.</param>
public class FeaturedCardService(IFeaturedCardStore store, CardCatalog catalog)
{
    private readonly IFeaturedCardStore _store = store;

    private readonly CardCatalog _catalog = catalog;

    private readonly Lock _lock = new();

    private FeaturedCardState _state = store.Load() ?? new FeaturedCardState(Card: null);

    /// <summary>
    /// The featured card, or <see langword="null"/> when nothing is featured.
    /// </summary>
    public Card? Current => _state.Card;

    /// <summary>
    /// Raised after the featured card changes. Handlers read <see cref="Current"/> for the new card.
    /// </summary>
    /// <remarks>
    /// May be raised on any thread.
    /// </remarks>
    public event Action? Changed;

    /// <summary>
    /// Puts a card on air, replacing whatever is featured.
    /// </summary>
    /// <param name="id">The card's id in the catalogue (<see cref="Card.Id"/>).</param>
    /// <returns><see langword="true"/> if the card is featured, and <see langword="false"/> if the catalogue has no card with that id.</returns>
    public bool Feature(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (_catalog.Find(id) is not { } card)
        {
            return false;
        }

        lock (_lock)
        {
            if (_state.Card == card)
            {
                return true;
            }

            Set(new FeaturedCardState(card));
        }

        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Takes the featured card off air. Does nothing if nothing is featured.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            if (_state.Card is null)
            {
                return;
            }

            Set(new FeaturedCardState(Card: null));
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Streams the featured card: the current state first, then each change to it, until cancelled.
    /// A burst of changes yields only the latest.
    /// </summary>
    /// <param name="cancellationToken">Ends the stream when cancelled, for example when the overlay disconnects.</param>
    /// <returns>The featured card, first as it is now and then after each change.</returns>
    public async IAsyncEnumerable<FeaturedCardState> WatchAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
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
            var last = _state;
            yield return last; // Initial state

            // Not cancellationToken: cancellation completes the channel (above), which ends this loop
            // normally. Passing the token would end it with an OperationCanceledException instead.
            await foreach (var _ in changed.Reader.ReadAllAsync(CancellationToken.None))
            {
                var current = _state;
                if (current == last)
                {
                    continue; // Changed and changed back before this caught up
                }

                last = current;
                yield return current;
            }
        }
        finally
        {
            Changed -= OnChanged;
        }
    }

    /// <summary>
    /// Updates the state and saves it.
    /// </summary>
    /// <remarks>
    /// Must be called inside the lock so that saves reach the store in the same order as the changes.
    /// </remarks>
    /// <param name="state">The state to assign.</param>
    private void Set(FeaturedCardState state)
    {
        _state = state;
        _store.Save(state);
    }
}
