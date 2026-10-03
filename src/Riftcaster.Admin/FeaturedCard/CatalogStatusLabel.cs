using System.Globalization;
using Riftcaster.Core.Cards;

namespace Riftcaster.Admin.FeaturedCard;

/// <summary>
/// How the card catalogue's state reads in the Featured card panel's meta, e.g.
/// "1,320 cards · updated 3h ago" or "Updating… page 4 of 15".
/// </summary>
/// <param name="Text">The meta text.</param>
/// <param name="Warn">Whether it's a problem, shown in the warning colour.</param>
/// <param name="Detail">More on the problem, for a tooltip, or <see langword="null"/>.</param>
public sealed record CatalogStatusLabel(string Text, bool Warn, string? Detail)
{
    /// <summary>
    /// Describes the catalogue's state.
    /// </summary>
    /// <param name="catalog">The catalogue.</param>
    /// <param name="now">The time now, for how long ago things happened.</param>
    public static CatalogStatusLabel For(CardCatalog catalog, DateTimeOffset now)
    {
        var status = catalog.Status;
        var count = catalog.Cards.Count;

        if (status.Fetching)
        {
            var verb = count == 0 ? "Fetching" : "Updating";
            return new(status.Progress is { } progress ? $"{verb}… page {progress.Page} of {progress.Pages}" : $"{verb}…", Warn: false, Detail: null);
        }

        if (status.LastFailure is { } failure)
        {
            var text = count == 0 ? "No cards · fetch failed" : $"{Cards(count)} · update failed";
            return new(text, Warn: true, $"Failed {Ago(now - failure.At)}: {failure.Reason}");
        }

        if (count == 0 || catalog.FetchedAt is not { } fetchedAt)
        {
            return new("No cards yet", Warn: false, Detail: null);
        }

        return new($"{Cards(count)} · updated {Ago(now - fetchedAt)}", Warn: false, Detail: null);
    }

    /// <summary>
    /// How long ago, roughly: "just now", "5 min ago", "3h ago" or "2 days ago".
    /// </summary>
    public static string Ago(TimeSpan elapsed) => elapsed switch
    {
        { TotalMinutes: < 1 } => "just now",
        { TotalHours: < 1 } => $"{(int)elapsed.TotalMinutes} min ago",
        { TotalDays: < 1 } => $"{(int)elapsed.TotalHours}h ago",
        { TotalDays: < 2 } => "1 day ago",
        _ => $"{(int)elapsed.TotalDays} days ago",
    };

    private static string Cards(int count) => string.Create(CultureInfo.InvariantCulture, $"{count:N0} cards");
}
