using Riftcaster.Contracts;

namespace Riftcaster.Core.Cards;

/// <summary>
/// The card databases the catalog can be fetched from, each with the store that keeps its own saved catalog, so
/// that switching back to one is instant.
/// </summary>
public sealed class CardSourceSet
{
    private readonly Dictionary<CardSourceKind, (ICardSource Source, ICardStore Store)> _entries;

    /// <summary>
    /// Creates the set.
    /// </summary>
    /// <param name="entries">Each source, with the store that keeps its catalog.</param>
    public CardSourceSet(params IEnumerable<(CardSourceKind Kind, ICardSource Source, ICardStore Store)> entries)
    {
        _entries = entries.ToDictionary(entry => entry.Kind, entry => (entry.Source, entry.Store));
    }

    /// <summary>
    /// Whether there is a source of that kind.
    /// </summary>
    /// <param name="kind">The kind.</param>
    public bool Has(CardSourceKind kind) => _entries.ContainsKey(kind);

    /// <summary>
    /// The source of a kind.
    /// </summary>
    /// <param name="kind">The kind.</param>
    public ICardSource Source(CardSourceKind kind) => _entries[kind].Source;

    /// <summary>
    /// The store that keeps the catalog of a kind of source.
    /// </summary>
    /// <param name="kind">The kind.</param>
    public ICardStore Store(CardSourceKind kind) => _entries[kind].Store;
}
