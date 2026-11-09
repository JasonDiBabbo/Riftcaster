using Riftcaster.Contracts;

namespace Riftcaster.Core.Cards;

/// <summary>
/// A fetch of the card catalog that didn't swap in new cards.
/// </summary>
/// <param name="At">When it failed.</param>
/// <param name="Reason">Why, in words the operator can read, e.g. the error message.</param>
/// <param name="Kind">What sort of failure, which decides how soon an automatic fetch may try again.</param>
/// <param name="Source">The source that couldn't be fetched, when it isn't the one in use (a switch to it), else null.</param>
public sealed record CardFetchFailure(DateTimeOffset At, string Reason, CardFailureKind Kind, CardSourceKind? Source = null);

/// <summary>
/// Why a fetch of the card catalog failed, as far as it matters to trying again.
/// </summary>
public enum CardFailureKind
{
    /// <summary>There was no answer: no connection, the name wasn't found, or the download was cut off. The source wasn't asked, or not in full, so trying again soon costs it nothing.</summary>
    NotReached,

    /// <summary>The source answered that it couldn't serve the request just now (a server error, "too many requests" or a timeout): trying again soon could add to its load.</summary>
    ServerError,

    /// <summary>The source answered with something unusable (a refusal, or an answer that isn't a card list): the same request would fail the same way.</summary>
    Unusable,
}
