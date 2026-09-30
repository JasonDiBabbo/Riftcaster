using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Riftcaster.Contracts;
using Riftcaster.Core.Storage;

namespace Riftcaster.Core.LowerThird;

/// <summary>
/// Saves the lower third entries to a JSON file, so they survive a server restart.
/// </summary>
/// <param name="path">The file to load from and save to. Its folder is created on the first save.</param>
/// <param name="logger">Logs a missing, unreadable or unwritable file, since those never throw.</param>
public sealed class JsonFileLowerThirdStore(string path, ILogger<JsonFileLowerThirdStore> logger) : ILowerThirdStore
{
    private readonly JsonDocumentFile<StoredLibrary> _file = new(path, logger);

    /// <inheritdoc/>
    public ImmutableList<LowerThirdEntry> Load() => _file.Read()?.Entries ?? [];

    /// <inheritdoc/>
    public void Save(ImmutableList<LowerThirdEntry> entries) => _file.Write(new StoredLibrary(entries));

    // The file's shape. An object rather than a bare array, so fields (such as a format version) can be added later.
    private sealed record StoredLibrary(ImmutableList<LowerThirdEntry> Entries);
}
