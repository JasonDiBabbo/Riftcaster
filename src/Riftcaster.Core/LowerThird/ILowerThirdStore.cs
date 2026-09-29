using System.Collections.Immutable;
using Riftcaster.Contracts;

namespace Riftcaster.Core.LowerThird;

/// <summary>
/// An interface for persisting lower third entries between server restarts.
/// What's on air is not persisted. After a restart, nothing is live.
/// </summary>
public interface ILowerThirdStore
{
    /// <summary>
    /// Loads the saved entries, newest first, or an empty list if there are none.
    /// </summary>
    /// <returns>The saved entries, newest first.</returns>
    ImmutableList<LowerThirdEntry> Load();

    /// <summary>
    /// Saves the entries, replacing any previous save.
    /// </summary>
    /// <remarks>
    /// Implementations should log failures rather than throw exceptions.
    /// </remarks>
    /// <param name="entries">The entries to save.</param>
    void Save(ImmutableList<LowerThirdEntry> entries);
}
