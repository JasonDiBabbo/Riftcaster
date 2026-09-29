namespace Riftcaster.Core.LowerThird;

/// <summary>
/// An in-memory store for lower third data.
/// </summary>
/// <remarks>
/// Data stored will not persist across server restarts.
/// </remarks>
public sealed class InMemoryLowerThirdStore : ILowerThirdStore
{
    /// <summary>
    /// The most recently saved library.
    /// </summary>
    public LowerThirdLibrary Library { get; private set; } = LowerThirdLibrary.Empty;

    /// <inheritdoc/>
    public LowerThirdLibrary Load() => Library;

    /// <inheritdoc/>
    public void Save(LowerThirdLibrary library) => Library = library;
}
