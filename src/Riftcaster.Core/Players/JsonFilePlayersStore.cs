using Microsoft.Extensions.Logging;
using Riftcaster.Contracts;
using Riftcaster.Core.Storage;

namespace Riftcaster.Core.Players;

/// <summary>
/// Saves the players and teams to a JSON file, so they survive a server restart.
/// </summary>
/// <param name="path">The file to load from and save to. Its folder is created on the first save.</param>
/// <param name="logger">Logs a missing, unreadable or unwritable file, since those never throw.</param>
public sealed class JsonFilePlayersStore(string path, ILogger<JsonFilePlayersStore> logger) : IPlayersStore
{
    private readonly JsonDocumentFile<PlayersState> _file = new(path, logger);

    /// <inheritdoc/>
    public PlayersState? Load() => _file.Read();

    /// <inheritdoc/>
    public void Save(PlayersState state) => _file.Write(state);
}
