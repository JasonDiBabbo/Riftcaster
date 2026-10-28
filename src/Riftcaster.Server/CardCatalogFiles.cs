namespace Riftcaster.Server;

/// <summary>
/// The files the card catalogs are saved in, one per source, in the data folder.
/// </summary>
internal static class CardCatalogFiles
{
    /// <summary>Riftcodex's catalog.</summary>
    public const string Riftcodex = "cards.riftcodex.json";

    /// <summary>RiftHunt's catalog.</summary>
    public const string RiftHunt = "cards.rifthunt.json";

    /// <summary>The sample that stands in for RiftHunt's in Development, kept apart so it can't be mistaken for the real one.</summary>
    public const string RiftHuntSample = "cards.rifthunt-sample.json";

    // What Riftcodex's catalog was called before there was another: every copy that has run has one of these.
    private const string Unnamed = "cards.json";

    /// <summary>
    /// The path of Riftcodex's catalog, after moving the one an earlier version saved as <c>cards.json</c>, which was
    /// always Riftcodex's, to it. Done once, when the catalogs are set up: nothing is lost by it, and nothing is moved
    /// if the new file is there already.
    /// </summary>
    /// <param name="dataDirectory">The data folder.</param>
    /// <param name="logger">Logs the move, and why it couldn't be done.</param>
    public static string RiftcodexPath(string dataDirectory, ILogger logger)
    {
        var current = Path.Combine(dataDirectory, Riftcodex);
        var old = Path.Combine(dataDirectory, Unnamed);

        if (File.Exists(old) && !File.Exists(current))
        {
            try
            {
                File.Move(old, current);
                logger.LogInformation("Renamed the saved card catalog {Old} to {New}: each card source has a file of its own now.", Unnamed, Riftcodex);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Riftcodex's catalog is fetched again, as for a copy that never had one.
                logger.LogWarning(exception, "Couldn't rename the saved card catalog {Old} to {New}; Riftcodex's catalog will be fetched again.", Unnamed, Riftcodex);
            }
        }

        return current;
    }
}
