using Microsoft.Extensions.Logging;
using Riftcaster.Contracts;
using Riftcaster.Core.Storage;

namespace Riftcaster.Core.Cards;

/// <summary>
/// What a card source remembers about its fetches between runs, so a source that may only be asked once a
/// day isn't asked again by starting Riftcaster again.
/// </summary>
/// <param name="LastAttemptAt">When the last fetch was started, or null if there has been none.</param>
/// <param name="NotBefore">The earliest time the source may be asked again, or null for any time.</param>
/// <param name="Failures">How many fetches in a row have failed, which lengthens the wait after the next one.</param>
public sealed record CardFetchRecord(DateTimeOffset? LastAttemptAt, DateTimeOffset? NotBefore, int Failures)
{
    /// <summary>Nothing remembered: the source has never been asked.</summary>
    public static readonly CardFetchRecord None = new(null, null, 0);
}

/// <summary>
/// Keeps each card source's <see cref="CardFetchRecord"/>.
/// </summary>
public interface ICardFetchHistory
{
    /// <summary>
    /// What is remembered about a source.
    /// </summary>
    /// <param name="source">The source.</param>
    CardFetchRecord Get(CardSourceKind source);

    /// <summary>
    /// Remembers a source's record, replacing the last.
    /// </summary>
    /// <remarks>
    /// Implementations should log failures rather than throw exceptions.
    /// </remarks>
    /// <param name="source">The source.</param>
    /// <param name="record">What to remember.</param>
    void Save(CardSourceKind source, CardFetchRecord record);
}

/// <summary>
/// Keeps the records in memory. Nothing survives a restart. For tests, and for sources that aren't live.
/// </summary>
public sealed class InMemoryCardFetchHistory : ICardFetchHistory
{
    private readonly Lock _lock = new();

    private readonly Dictionary<CardSourceKind, CardFetchRecord> _records = [];

    /// <inheritdoc/>
    public CardFetchRecord Get(CardSourceKind source)
    {
        lock (_lock)
        {
            return _records.GetValueOrDefault(source, CardFetchRecord.None);
        }
    }

    /// <inheritdoc/>
    public void Save(CardSourceKind source, CardFetchRecord record)
    {
        lock (_lock)
        {
            _records[source] = record;
        }
    }
}

/// <summary>
/// Saves the records to a JSON file, so they outlast a restart.
/// </summary>
/// <param name="path">The file to load from and save to. Its folder is created on the first save.</param>
/// <param name="logger">Logs a missing, unreadable or unwritable file, since those never throw.</param>
public sealed class JsonFileCardFetchHistory(string path, ILogger<JsonFileCardFetchHistory> logger) : ICardFetchHistory
{
    private readonly JsonDocumentFile<Document> _file = new(path, logger);

    private readonly Lock _lock = new();

    private Dictionary<CardSourceKind, CardFetchRecord>? _records;

    /// <inheritdoc/>
    public CardFetchRecord Get(CardSourceKind source)
    {
        lock (_lock)
        {
            return Records().GetValueOrDefault(source, CardFetchRecord.None);
        }
    }

    /// <inheritdoc/>
    public void Save(CardSourceKind source, CardFetchRecord record)
    {
        lock (_lock)
        {
            var records = Records();
            records[source] = record;
            _file.Write(new Document(records.ToDictionary(entry => entry.Key.ToString(), entry => entry.Value)));
        }
    }

    // Read once, then kept: this is the only writer.
    private Dictionary<CardSourceKind, CardFetchRecord> Records() => _records ??= Load();

    private Dictionary<CardSourceKind, CardFetchRecord> Load()
    {
        var records = new Dictionary<CardSourceKind, CardFetchRecord>();
        foreach (var (name, record) in _file.Read()?.Sources ?? [])
        {
            // A source this version doesn't know (saved by a later one) is left out.
            if (Enum.TryParse<CardSourceKind>(name, out var source) && Enum.IsDefined(source))
            {
                records[source] = record;
            }
        }

        return records;
    }

    private sealed record Document(Dictionary<string, CardFetchRecord> Sources);
}
