namespace Riftcaster.Core.Cards;

/// <summary>
/// Keeps the last fetched card catalogue, so it's there at startup and without internet.
/// </summary>
public interface ICardStore
{
    /// <summary>
    /// Loads the saved catalogue.
    /// </summary>
    /// <returns>
    /// The saved catalogue, or <see langword="null"/> if there is none (the catalogue then starts
    /// empty until its first fetch).
    /// </returns>
    CardCatalogSnapshot? Load();

    /// <summary>
    /// Saves the catalogue, replacing any previous save.
    /// </summary>
    /// <remarks>
    /// Implementations should log failures rather than throw exceptions.
    /// </remarks>
    void Save(CardCatalogSnapshot snapshot);
}
