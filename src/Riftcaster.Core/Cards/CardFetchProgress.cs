namespace Riftcaster.Core.Cards;

/// <summary>
/// How far a fetch of the card catalog has got.
/// </summary>
/// <param name="Page">The pages fetched so far, from 1.</param>
/// <param name="Pages">How many pages the whole catalog takes.</param>
public sealed record CardFetchProgress(int Page, int Pages);
