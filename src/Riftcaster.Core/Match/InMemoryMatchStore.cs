using Riftcaster.Contracts;

namespace Riftcaster.Core.Match;

/// <summary>
/// An in-memory store for the match settings.
/// </summary>
/// <remarks>
/// Nothing survives a server restart. For tests.
/// </remarks>
public sealed class InMemoryMatchStore : IMatchStore
{
    /// <summary>The most recently saved settings.</summary>
    public MatchSettings? Settings { get; private set; }

    /// <summary>How many times <see cref="Save"/> has been called, so tests can check when the service saves.</summary>
    public int SaveCount { get; private set; }

    /// <inheritdoc/>
    public MatchSettings? Load() => Settings;

    /// <inheritdoc/>
    public void Save(MatchSettings settings)
    {
        Settings = settings;
        SaveCount++;
    }
}
