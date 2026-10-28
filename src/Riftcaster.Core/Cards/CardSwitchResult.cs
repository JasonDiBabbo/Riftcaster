namespace Riftcaster.Core.Cards;

/// <summary>
/// How a switch to another card source ended.
/// </summary>
public enum CardSwitchOutcome
{
    /// <summary>The catalog is now in use from the new source.</summary>
    Switched,

    /// <summary>It already was.</summary>
    AlreadyActive,

    /// <summary>There is no such source.</summary>
    UnknownSource,

    /// <summary>The catalog is fetching, from the source in use or another: a switch waits for that.</summary>
    Busy,

    /// <summary>The new source couldn't be fetched, and the catalog is as it was.</summary>
    Failed,

    /// <summary>The new source may not be asked yet (see <see cref="ICardSource.NotBefore"/>), and the catalog is as it was.</summary>
    TooSoon,

    /// <summary>It was canceled, and the catalog is as it was.</summary>
    Canceled,
}

/// <summary>
/// How a switch to another card source ended, and why if it didn't work.
/// </summary>
/// <param name="Outcome">How it ended.</param>
/// <param name="Problem">What went wrong, in words for the operator, or null.</param>
/// <param name="NotBefore">When the source may be asked, for <see cref="CardSwitchOutcome.TooSoon"/>.</param>
public sealed record CardSwitchResult(CardSwitchOutcome Outcome, string? Problem = null, DateTimeOffset? NotBefore = null)
{
    /// <summary>Whether the catalog is in use from the source asked for.</summary>
    public bool Done => Outcome is CardSwitchOutcome.Switched or CardSwitchOutcome.AlreadyActive;
}
