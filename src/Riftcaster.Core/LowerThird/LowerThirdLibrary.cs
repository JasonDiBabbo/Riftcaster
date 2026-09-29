using System.Collections.Immutable;
using Riftcaster.Contracts;

namespace Riftcaster.Core.LowerThird;

/// <summary>
/// An immutable snapshot of the lower third library.
/// </summary>
/// <param name="Entries">The entries in the library.</param>
/// <param name="LiveEntryId">The live entry unique identifier, if any.</param>
public sealed record LowerThirdLibrary(ImmutableList<LowerThirdEntry> Entries, Guid? LiveEntryId)
{
    public static readonly LowerThirdLibrary Empty = new([], null);

    /// <summary>
    /// The live entry's message, or null when nothing is showing.
    /// </summary>
    public LowerThirdMessage? LiveMessage => LiveEntryId is { } id ? Entries.Find(entry => entry.Id == id)?.Message : null;
}
