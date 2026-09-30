using Microsoft.Extensions.Logging;
using Riftcaster.Contracts;
using Riftcaster.Core.Storage;

namespace Riftcaster.Core.Match;

/// <summary>
/// Saves the match settings to a JSON file, so they survive a server restart.
/// </summary>
/// <param name="path">The file to load from and save to. Its folder is created on the first save.</param>
/// <param name="logger">Logs a missing, unreadable or unwritable file, since those never throw.</param>
public sealed class JsonFileMatchStore(string path, ILogger<JsonFileMatchStore> logger) : IMatchStore
{
    private readonly JsonDocumentFile<MatchSettings> _file = new(path, logger);

    /// <inheritdoc/>
    public MatchSettings? Load() => _file.Read();

    /// <inheritdoc/>
    public void Save(MatchSettings settings) => _file.Write(settings);
}
