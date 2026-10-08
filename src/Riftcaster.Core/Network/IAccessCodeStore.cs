namespace Riftcaster.Core.Network;

/// <summary>
/// Keeps the access code between restarts.
/// </summary>
public interface IAccessCodeStore
{
    /// <summary>
    /// Loads the saved code.
    /// </summary>
    /// <returns>The saved code, if there's one and it can be read.</returns>
    LoadedAccessCode Load();

    /// <summary>
    /// Saves the code, replacing any previous one, or removes it.
    /// </summary>
    /// <remarks>
    /// Implementations should log failures rather than throw exceptions.
    /// </remarks>
    /// <param name="code">The code to save, or <see langword="null"/> to remove it.</param>
    void Save(StoredAccessCode? code);
}
