using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Riftcaster.Contracts;

namespace Riftcaster.Core.LowerThird;

/// <summary>
/// An immutable snapshot of the lower third library.
/// </summary>
/// <param name="Entries">The entries in the library, newest first.</param>
/// <param name="LiveEntryId">The id of the entry on air, or <see langword="null" /> if nothing is showing.</param>
public sealed record LowerThirdLibrary(ImmutableList<LowerThirdEntry> Entries, Guid? LiveEntryId)
{
    /// <summary>
    /// The live entry's message, or <see langword="null" /> when nothing is showing.
    /// </summary>
    public LowerThirdMessage? LiveMessage => LiveEntryId is { } id ? Entries.Find(entry => entry.Id == id)?.Message : null;
}
