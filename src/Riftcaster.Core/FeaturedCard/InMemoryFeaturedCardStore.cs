using Riftcaster.Contracts;

namespace Riftcaster.Core.FeaturedCard;

/// <summary>
/// An in-memory store for the featured card.
/// </summary>
/// <remarks>
/// Nothing survives a server restart. For tests.
/// </remarks>
public sealed class InMemoryFeaturedCardStore : IFeaturedCardStore
{
    /// <summary>
    /// The most recently saved state.
    /// </summary>
    public FeaturedCardState? State { get; private set; }

    /// <summary>
    /// How many times <see cref="Save"/> has been called, so tests can check when the service saves.
    /// </summary>
    public int SaveCount { get; private set; }

    /// <inheritdoc/>
    public FeaturedCardState? Load() => State;

    /// <inheritdoc/>
    public void Save(FeaturedCardState state)
    {
        State = state;
        SaveCount++;
    }
}
