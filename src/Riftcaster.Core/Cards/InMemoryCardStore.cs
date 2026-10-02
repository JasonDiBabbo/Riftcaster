namespace Riftcaster.Core.Cards;

/// <summary>
/// An in-memory store for the card catalogue.
/// </summary>
/// <remarks>
/// Nothing survives a server restart. For tests.
/// </remarks>
public sealed class InMemoryCardStore : ICardStore
{
    /// <summary>
    /// The most recently saved catalogue.
    /// </summary>
    public CardCatalogSnapshot? Snapshot { get; private set; }

    /// <summary>
    /// How many times <see cref="Save"/> has been called, so tests can check when the catalogue saves.
    /// </summary>
    public int SaveCount { get; private set; }

    /// <inheritdoc/>
    public CardCatalogSnapshot? Load() => Snapshot;

    /// <inheritdoc/>
    public void Save(CardCatalogSnapshot snapshot)
    {
        Snapshot = snapshot;
        SaveCount++;
    }
}
