using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Riftcaster.Contracts;

namespace Riftcaster.Core.LowerThird;

/// <summary>
/// Saves the lower third entries to a JSON file, so they survive a server restart.
/// </summary>
/// <param name="path">The file to load from and save to. Its folder is created on the first save.</param>
/// <param name="logger">Logs a missing, unreadable or unwritable file, since those never throw.</param>
public sealed class JsonFileLowerThirdStore(string path, ILogger<JsonFileLowerThirdStore> logger) : ILowerThirdStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,

        // Reject entries with missing or null values (e.g. a Socials message with no links) instead of
        // loading nulls that would crash later. The file is then treated as invalid and backed up.
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
    };

    private readonly string _path = Path.GetFullPath(path);

    private readonly ILogger<JsonFileLowerThirdStore> _logger = logger;

    /// <inheritdoc/>
    /// <remarks>
    /// If the file can't be parsed, it's moved aside to a <c>.bad</c> file first,
    /// so the next save doesn't overwrite a library that might be recoverable by hand.
    /// </remarks>
    public ImmutableList<LowerThirdEntry> Load()
    {
        if (!File.Exists(_path))
        {
            _logger.LogInformation("No saved lower thirds at {Path}; starting empty.", _path);
            return [];
        }

        try
        {
            StoredLibrary? library;
            using (var stream = File.OpenRead(_path))
            {
                library = JsonSerializer.Deserialize<StoredLibrary>(stream, SerializerOptions);
            }

            // "null" and "{}" are valid JSON but not a library: the result is null, or has no entries.
            if (library?.Entries is not null)
            {
                return library.Entries;
            }
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            // JsonException: not valid JSON, or an unknown message type.
            // NotSupportedException: a message with no "type" (the abstract base can't be created).
            _logger.LogError(exception, "Saved lower thirds at {Path} are not valid.", _path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The file exists but can't be read (locked, or no permission). Leave it alone.
            _logger.LogError(exception, "Could not read saved lower thirds at {Path}; starting empty.", _path);
            return [];
        }

        var backupPath = _path + ".bad";
        try
        {
            File.Move(_path, backupPath, overwrite: true);
            _logger.LogWarning("Moved unreadable lower thirds to {BackupPath}; starting empty.", backupPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(exception, "Could not back up unreadable lower thirds at {Path}; starting empty.", _path);
        }

        return [];
    }

    /// <inheritdoc/>
    public void Save(ImmutableList<LowerThirdEntry> entries)
    {
        var tempPath = _path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

            // Write the whole file, then swap it in, so a crash mid-save leaves the old file intact.
            using (var stream = File.Create(tempPath))
            {
                JsonSerializer.Serialize(stream, new StoredLibrary(entries), SerializerOptions);
            }

            File.Move(tempPath, _path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(exception, "Could not save lower thirds to {Path}.", _path);
        }
    }

    // The file's shape. An object rather than a bare array, so fields (such as a format version) can be added later.
    private sealed record StoredLibrary(ImmutableList<LowerThirdEntry> Entries);
}
