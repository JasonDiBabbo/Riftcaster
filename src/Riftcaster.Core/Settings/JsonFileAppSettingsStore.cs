using Microsoft.Extensions.Logging;
using Riftcaster.Contracts;
using Riftcaster.Core.Storage;

namespace Riftcaster.Core.Settings;

/// <summary>
/// Saves the Settings page's settings to a JSON file, so they survive a server restart.
/// </summary>
/// <param name="path">The file to load from and save to. Its folder is created on the first save.</param>
/// <param name="logger">Logs a missing, unreadable or unwritable file, since those never throw.</param>
public sealed class JsonFileAppSettingsStore(string path, ILogger<JsonFileAppSettingsStore> logger) : IAppSettingsStore
{
    private readonly JsonDocumentFile<AppSettings> _file = new(path, logger);

    /// <inheritdoc/>
    public AppSettings? Load() => _file.Read();

    /// <inheritdoc/>
    public void Save(AppSettings settings) => _file.Write(settings);
}
