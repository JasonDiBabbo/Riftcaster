using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Riftcaster.Core.Storage;

/// <summary>
/// One JSON document on disk, read and written safely. Shared by the file-backed stores.
/// </summary>
/// <remarks>
/// Never throws for file or JSON problems; it logs them instead, so a bad file can't take
/// the server down or break the operator's click:
/// <list type="bullet">
/// <item>Writes go to a temporary file that's then moved into place, so a crash mid-save
/// leaves the previous file intact.</item>
/// <item>A missing file reads as <see langword="null"/>.</item>
/// <item>An unreadable one (invalid JSON, missing or null values) is moved aside to a
/// <c>.bad</c> file and reads as <see langword="null"/>, so the next write can't overwrite
/// something that might be recoverable by hand.</item>
/// </list>
/// </remarks>
/// <typeparam name="T">The document's type.</typeparam>
/// <param name="path">The file. Its folder is created on the first write.</param>
/// <param name="logger">The owning store's logger.</param>
public sealed class JsonDocumentFile<T>(string path, ILogger logger) where T : class
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,

        // Reject documents with missing or null values (e.g. a Socials message with no links) instead of
        // loading nulls that would crash later. The file is then treated as invalid and backed up.
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
    };

    private readonly string _path = Path.GetFullPath(path);

    private readonly ILogger _logger = logger;

    /// <summary>
    /// Reads the document.
    /// </summary>
    /// <returns>
    /// The document, or <see langword="null"/> if the file is missing or unreadable.
    /// </returns>
    public T? Read()
    {
        if (!File.Exists(_path))
        {
            _logger.LogInformation("No saved data at {Path}; starting fresh.", _path);
            return null;
        }

        try
        {
            T? document;
            using (var stream = File.OpenRead(_path))
            {
                document = JsonSerializer.Deserialize<T>(stream, SerializerOptions);
            }

            if (document is not null)
            {
                return document;
            }

            // The file contains just "null": valid JSON, but not a document.
            _logger.LogError("Saved data at {Path} is empty.", _path);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            // JsonException: not valid JSON, missing or null values, or an unknown enum or message type.
            // NotSupportedException: a message with no "type" (the abstract base can't be created).
            _logger.LogError(exception, "Saved data at {Path} is not valid.", _path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The file exists but can't be read (locked, or no permission). Leave it alone.
            _logger.LogError(exception, "Could not read saved data at {Path}; starting fresh.", _path);
            return null;
        }

        MoveAside();
        return null;
    }

    /// <summary>Writes the document, replacing the file.</summary>
    public void Write(T document)
    {
        var tempPath = _path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

            // Write the whole file, then swap it in, so a crash mid-save leaves the old file intact.
            using (var stream = File.Create(tempPath))
            {
                JsonSerializer.Serialize(stream, document, SerializerOptions);
            }

            File.Move(tempPath, _path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(exception, "Could not save data to {Path}.", _path);
        }
    }

    private void MoveAside()
    {
        var backupPath = _path + ".bad";
        try
        {
            File.Move(_path, backupPath, overwrite: true);
            _logger.LogWarning("Moved unreadable data to {BackupPath}; starting fresh.", backupPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(exception, "Could not back up unreadable data at {Path}; starting fresh.", _path);
        }
    }
}
