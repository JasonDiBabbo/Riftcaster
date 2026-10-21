using Riftcaster.Contracts;

namespace Riftcaster.Core.Settings;

/// <summary>
/// An in-memory store for the Settings page's settings.
/// </summary>
/// <remarks>
/// Nothing survives a server restart. For tests.
/// </remarks>
public sealed class InMemoryAppSettingsStore : IAppSettingsStore
{
    /// <summary>The most recently saved settings.</summary>
    public AppSettings? Settings { get; private set; }

    /// <summary>How many times <see cref="Save"/> has been called, so tests can check when the service saves.</summary>
    public int SaveCount { get; private set; }

    /// <inheritdoc/>
    public AppSettings? Load() => Settings;

    /// <inheritdoc/>
    public void Save(AppSettings settings)
    {
        Settings = settings;
        SaveCount++;
    }
}
