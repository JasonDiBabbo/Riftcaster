using Riftcaster.Contracts;

namespace Riftcaster.Core.Cards;

/// <summary>
/// A fetch of the card catalog that didn't swap in new cards.
/// </summary>
/// <param name="At">When it failed.</param>
/// <param name="Reason">Why, in words the operator can read, e.g. the error message.</param>
/// <param name="Source">The source that couldn't be fetched, when it isn't the one in use (a switch to it), else null.</param>
public sealed record CardFetchFailure(DateTimeOffset At, string Reason, CardSourceKind? Source = null);
