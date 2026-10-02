using Microsoft.Extensions.Logging;
using Riftcaster.Core.Storage;

namespace Riftcaster.Core.Cards;

/// <summary>
/// Saves the card catalogue to a JSON file, so it's there at startup and without internet.
/// </summary>
/// <param name="path">The file to load from and save to. Its folder is created on the first save.</param>
/// <param name="logger">Logs a missing, unreadable or unwritable file, since those never throw.</param>
public sealed class JsonFileCardStore(string path, ILogger<JsonFileCardStore> logger) : ICardStore
{
    private readonly JsonDocumentFile<CardCatalogSnapshot> _file = new(path, logger);

    /// <inheritdoc/>
    public CardCatalogSnapshot? Load() => _file.Read();

    /// <inheritdoc/>
    public void Save(CardCatalogSnapshot snapshot) => _file.Write(snapshot);
}
