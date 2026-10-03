namespace Riftcaster.Core.Cards;

/// <summary>
/// What the card catalogue is doing, for the admin to show.
/// </summary>
/// <param name="Fetching">Whether a fetch is running.</param>
/// <param name="Progress">
/// How far the running fetch has got, or <see langword="null"/> before its first page arrives (or when none is running).
/// </param>
/// <param name="LastFailure">
/// Why the last fetch didn't swap in new cards, or <see langword="null"/> if it did (or none has finished yet).
/// </param>
public sealed record CardCatalogStatus(bool Fetching, CardFetchProgress? Progress, CardFetchFailure? LastFailure)
{
    /// <summary>
    /// Not fetching, and the last fetch (if any) worked.
    /// </summary>
    public static readonly CardCatalogStatus Idle = new(Fetching: false, Progress: null, LastFailure: null);
}
