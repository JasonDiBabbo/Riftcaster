using System.Collections.Immutable;
using Riftcaster.Contracts;

namespace Riftcaster.Core.LowerThird;

/// <summary>
/// An in-memory store for lower third data.
/// </summary>
/// <remarks>
/// Data stored will not persist across server restarts.
/// </remarks>
public sealed class InMemoryLowerThirdStore : ILowerThirdStore
{
    /// <summary>The most recently saved entries.</summary>
    public ImmutableList<LowerThirdEntry> Entries { get; private set; } = [];

    /// <summary>How many times <see cref="Save"/> has been called, so tests can check when the service saves.</summary>
    public int SaveCount { get; private set; }

    /// <inheritdoc/>
    public ImmutableList<LowerThirdEntry> Load() => Entries;

    /// <inheritdoc/>
    public void Save(ImmutableList<LowerThirdEntry> entries)
    {
        Entries = entries;
        SaveCount++;
    }
}
