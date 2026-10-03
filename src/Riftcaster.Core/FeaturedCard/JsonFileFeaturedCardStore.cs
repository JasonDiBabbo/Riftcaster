using Microsoft.Extensions.Logging;
using Riftcaster.Contracts;
using Riftcaster.Core.Storage;

namespace Riftcaster.Core.FeaturedCard;

/// <summary>
/// Saves the featured card to a JSON file, so it survives a server restart.
/// </summary>
/// <param name="path">The file to load from and save to. Its folder is created on the first save.</param>
/// <param name="logger">Logs a missing, unreadable or unwritable file, since those never throw.</param>
public sealed class JsonFileFeaturedCardStore(string path, ILogger<JsonFileFeaturedCardStore> logger) : IFeaturedCardStore
{
    private readonly JsonDocumentFile<FeaturedCardState> _file = new(path, logger);

    /// <inheritdoc/>
    public FeaturedCardState? Load() => _file.Read();

    /// <inheritdoc/>
    public void Save(FeaturedCardState state) => _file.Write(state);
}
