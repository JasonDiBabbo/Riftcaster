namespace Riftcaster.Contracts;

/// <summary>
/// A saved lower third in the library.
/// </summary>
/// <param name="Id">The unique identifier of the entry.</param>
/// <param name="Message">The entry content.</param>
public record LowerThirdEntry(Guid Id, LowerThirdMessage Message);
