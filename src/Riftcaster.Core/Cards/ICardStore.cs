namespace Riftcaster.Core.Cards;

/// <summary>
/// Keeps the last fetched card catalog, so it's there at startup and without internet.
/// </summary>
public interface ICardStore
{
    /// <summary>
    /// Loads the saved catalog.
    /// </summary>
    /// <returns>
    /// The saved catalog, or <see langword="null"/> if there is none (the catalog then starts
    /// empty until its first fetch).
    /// </returns>
    CardCatalogSnapshot? Load();

    /// <summary>
    /// Saves the catalog, replacing any previous save.
    /// </summary>
    /// <remarks>
    /// Implementations should log failures rather than throw exceptions.
    /// </remarks>
    void Save(CardCatalogSnapshot snapshot);
}
