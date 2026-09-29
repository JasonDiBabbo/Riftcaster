namespace Riftcaster.Core.LowerThird;

/// <summary>
/// An interface for persisting lower third libraries between server restarts.
/// </summary>
public interface ILowerThirdStore
{
    /// <summary>
    /// Loads the saved library or <see cref="LowerThirdLibrary.Empty"/> if there isn't one.
    /// </summary>
    /// <returns>The saved library.</returns>
    LowerThirdLibrary Load();

    /// <summary>
    /// Saves the library, replacing any previous save.
    /// </summary>
    /// <remarks>
    /// Implementations should log failures rather than throw exceptions.
    /// </remarks>
    /// <param name="library">The library to save.</param>
    void Save(LowerThirdLibrary library);
}
