namespace Riftcaster.Core.Cards;

/// <summary>
/// A fetch of the card catalogue that didn't swap in new cards.
/// </summary>
/// <param name="At">When it failed.</param>
/// <param name="Reason">Why, in words the operator can read, e.g. the error message.</param>
public sealed record CardFetchFailure(DateTimeOffset At, string Reason);
