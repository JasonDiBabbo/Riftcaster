using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Riftcaster.Contracts;
using Riftcaster.Core.Cards;
using Riftcaster.Server.Api;

namespace Riftcaster.Server;

public static class CardCatalogEndpoints
{
    public static WebApplication MapCardCatalog(this WebApplication app)
    {
        // REST (issue #9): the Settings page's card catalog section.
        var catalog = app.MapGroup("/api/cards/catalog").WithTags("Cards");

        catalog.MapGet("/", (CardCatalog cards) => TypedResults.Ok(CardCatalogInfo.Of(cards)))
            .WithSummary("Get the state of the card catalog")
            .WithDescription("Which card database it's from, how many cards it has and when they were fetched, whether a fetch is running, and why the last one failed, if it did.");

        catalog.MapPut("/source", async Task<Results<Ok<CardCatalogInfo>, Accepted<CardCatalogInfo>, Conflict<ProblemDetails>, ValidationProblem>> (CardSourceChoice choice, CardCatalog cards, ILogger<CardCatalog> logger) =>
            {
                if (choice.Source is not { } source || !Enum.IsDefined(source) || !cards.HasSource(source))
                {
                    return ApiProblems.Invalid("source", "Use Riftcodex or RiftHunt.");
                }

                if (source == cards.ActiveSource)
                {
                    return TypedResults.Ok(CardCatalogInfo.Of(cards));
                }

                if (cards.Status.Fetching)
                {
                    return ApiProblems.Conflict("The card catalog is being fetched. Try again when it's done.");
                }

                // A catalog that's saved is in use at once.
                if (cards.HasSavedCatalog(source))
                {
                    await cards.SwitchSourceAsync(source);
                    return TypedResults.Ok(CardCatalogInfo.Of(cards));
                }

                if (cards.NotBeforeFor(source) is { } notBefore)
                {
                    return ApiProblems.Conflict($"{source} may not be asked for its catalog until {notBefore:u}.");
                }

                // Fetched in the background: it can take minutes, and the answer to this request says it has begun.
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await cards.SwitchSourceAsync(source);
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(exception, "Switching to the {Source} card catalog failed unexpectedly.", source);
                    }
                });
                return TypedResults.Accepted((string?)null, CardCatalogInfo.Of(cards));
            })
            .WithSummary("Switch the card catalog to another source")
            .WithDescription("A source whose catalog is already saved is in use at once (200). Otherwise its catalog is fetched in the background (202; follow it with GET /api/cards/catalog) while the catalog in use stays as it is, and the source is switched to once the fetch has worked. 409 if a fetch is running, or the source may not be asked yet: RiftHunt asks to be fetched at most once a day.");

        catalog.MapDelete("/source", (CardCatalog cards) =>
            {
                cards.CancelSwitch();
                return TypedResults.Ok(CardCatalogInfo.Of(cards));
            })
            .WithSummary("Cancel a switch of the card source")
            .WithDescription("Stops a switch that is fetching the new source's catalog. The catalog stays as it was. Does nothing if none is.");

        catalog.MapPost("/refresh", Results<Accepted<CardCatalogInfo>, Conflict<ProblemDetails>> (CardCatalog cards, ILogger<CardCatalog> logger) =>
            {
                if (cards.Status.Fetching)
                {
                    return ApiProblems.Conflict("The card catalog is already being fetched.");
                }

                if (cards.IsSample(cards.ActiveSource))
                {
                    return ApiProblems.Conflict($"The {cards.ActiveSource} catalog is a built-in sample for Development, so there is nothing to download. Start with --{Program.RiftHuntLiveSetting} true for the real one.");
                }

                if (cards.NotBefore is { } notBefore)
                {
                    return ApiProblems.Conflict($"{cards.ActiveSource} may not be asked for its catalog again until {notBefore:u}.");
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await cards.RefreshAsync(CancellationToken.None);
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(exception, "Refreshing the card catalog failed unexpectedly.");
                    }
                });
                return TypedResults.Accepted((string?)null, CardCatalogInfo.Of(cards));
            })
            .WithSummary("Fetch the card catalog again")
            .WithDescription("Fetches a new catalog from the source in use, in the background (202; follow it with GET /api/cards/catalog). 409 if a fetch is running, the source may not be asked yet, or the catalog is the built-in sample that stands in for RiftHunt in Development.");

        return app;
    }
}

/// <summary>
/// The card source to switch to.
/// </summary>
/// <param name="Source">Riftcodex or RiftHunt.</param>
public record CardSourceChoice(CardSourceKind? Source);

/// <summary>
/// The state of the card catalog.
/// </summary>
/// <param name="Source">The card database the catalog is in use from.</param>
/// <param name="CardCount">How many cards it has.</param>
/// <param name="FetchedAt">When they were fetched, or null if there are none yet.</param>
/// <param name="Fetching">Whether a fetch is running.</param>
/// <param name="SwitchingTo">The source being fetched to switch to, or null if no switch is running.</param>
/// <param name="NotBefore">When the source in use may next be asked for its catalog, or null if it may be now.</param>
/// <param name="IsSample">Whether the catalog is a small built-in sample standing in for the real one, as RiftHunt's is in Development.</param>
/// <param name="LastFailure">Why the last fetch or switch didn't work, or null if it did (or none has finished yet).</param>
public record CardCatalogInfo(
    CardSourceKind Source,
    int CardCount,
    DateTimeOffset? FetchedAt,
    bool Fetching,
    CardSourceKind? SwitchingTo,
    DateTimeOffset? NotBefore,
    bool IsSample,
    CardCatalogFailure? LastFailure)
{
    internal static CardCatalogInfo Of(CardCatalog catalog) => new(
        catalog.ActiveSource,
        catalog.Cards.Count,
        catalog.FetchedAt,
        catalog.Status.Fetching,
        catalog.Status.SwitchingTo,
        catalog.NotBefore,
        catalog.IsSample(catalog.ActiveSource),
        catalog.Status.LastFailure is { } failure ? new CardCatalogFailure(failure.At, failure.Reason, failure.Source) : null);
}

/// <summary>
/// A fetch of the card catalog that didn't work.
/// </summary>
/// <param name="At">When it failed.</param>
/// <param name="Reason">Why.</param>
/// <param name="Source">The source that couldn't be fetched, if it wasn't the one in use (a switch to it).</param>
public record CardCatalogFailure(DateTimeOffset At, string Reason, CardSourceKind? Source);
