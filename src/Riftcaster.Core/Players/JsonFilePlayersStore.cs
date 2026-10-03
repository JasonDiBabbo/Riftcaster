using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Riftcaster.Contracts;
using Riftcaster.Core.Storage;

namespace Riftcaster.Core.Players;

/// <summary>
/// Saves the players and teams to a JSON file, so they survive a server restart.
/// </summary>
/// <remarks>
/// Also reads files saved before players held card snapshots, when legends, champions and
/// battlefields were saved as names. Those names are dropped (the operator chooses the cards again
/// from the catalogue), and the rest of each player is kept. The next save writes the new shape.
/// </remarks>
public sealed class JsonFilePlayersStore : IPlayersStore
{
    private readonly ILogger<JsonFilePlayersStore> _logger;

    private readonly CardOrOldNameConverter _cards = new();

    private readonly JsonDocumentFile<PlayersState> _file;

    /// <summary>
    /// Creates the store.
    /// </summary>
    /// <param name="path">The file to load from and save to. Its folder is created on the first save.</param>
    /// <param name="logger">Logs a missing, unreadable or unwritable file, since those never throw, and dropped old names.</param>
    public JsonFilePlayersStore(string path, ILogger<JsonFilePlayersStore> logger)
    {
        _logger = logger;
        _file = new(path, logger, _cards);
    }

    /// <inheritdoc/>
    public PlayersState? Load()
    {
        var before = _cards.OldNames;
        var state = _file.Read();

        if (_cards.OldNames > before)
        {
            _logger.LogWarning(
                "Dropped {Count} legend, champion or battlefield names saved by an earlier version; choose those cards again.",
                _cards.OldNames - before);
        }

        return state;
    }

    /// <inheritdoc/>
    public void Save(PlayersState state) => _file.Write(state);

    /// <summary>
    /// Reads a card, or a card name saved by an earlier version as no card, counting those.
    /// Writes cards as usual.
    /// </summary>
    private sealed class CardOrOldNameConverter : JsonConverter<Card>
    {
        // The same options without this converter, to read and write the card itself without
        // coming back here.
        private JsonSerializerOptions? _inner;

        /// <summary>
        /// How many old names have been read and dropped.
        /// </summary>
        public int OldNames { get; private set; }

        public override Card? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                OldNames++;
                return null;
            }

            return JsonSerializer.Deserialize<Card>(ref reader, Inner(options));
        }

        public override void Write(Utf8JsonWriter writer, Card value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value, Inner(options));

        private JsonSerializerOptions Inner(JsonSerializerOptions options)
        {
            if (_inner is null)
            {
                var inner = new JsonSerializerOptions(options);
                inner.Converters.Remove(this);
                _inner = inner;
            }

            return _inner;
        }
    }
}
