using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Riftcaster.Contracts;
using Riftcaster.Core.Cards;
using Riftcaster.Core.FeaturedCard;
using Riftcaster.Server.Api;

namespace Riftcaster.Server;

public static class FeaturedCardEndpoints
{
    /// <summary>
    /// The most cards one search returns.
    /// </summary>
    public const int MaxSearchLimit = 100;

    public static WebApplication MapFeaturedCard(this WebApplication app)
    {
        app.MapGet("/api/featured-card/events",
            (HttpContext context, FeaturedCardService featuredCard, IHostApplicationLifetime lifetime) =>
                StateSockets.SendStatesAsync(context, featuredCard.WatchAsync, lifetime.ApplicationStopping))
            .ExcludeFromDescription(); // A WebSocket, which OpenAPI can't describe

        // REST (issue #9): the admin's Featured card panel, and the card catalogue it searches.
        var featured = app.MapGroup("/api/featured-card").WithTags("Featured card");

        featured.MapGet("/", (FeaturedCardService service) => TypedResults.Ok(new FeaturedCardState(service.Current)))
            .WithSummary("Get the featured card");

        featured.MapPut("/", Results<Ok<FeaturedCardState>, ValidationProblem> (FeaturedCardChoice choice, FeaturedCardService service, CardCatalog catalog) =>
                service.Feature(choice.CardId!)
                    ? TypedResults.Ok(new FeaturedCardState(service.Current))
                    : ApiProblems.Invalid("cardId", catalog.Cards.Count == 0
                        ? "The card catalogue hasn't loaded yet. Try again in a minute."
                        : $"No card with id '{choice.CardId}'. Find ids with GET /api/cards?search=..."))
            .WithSummary("Feature a card")
            .WithDescription("Shows it on the Featured card overlay, in place of any card shown already.");

        featured.MapDelete("/", (FeaturedCardService service) =>
            {
                service.Clear();
                return TypedResults.Ok(new FeaturedCardState(service.Current));
            })
            .WithSummary("Clear the featured card")
            .WithDescription("Takes the card off the overlay. Does nothing if no card is featured.");

        var cards = app.MapGroup("/api/cards").WithTags("Cards");

        cards.MapGet("/", ([Required] string search, CardCatalog catalog, [Range(1, MaxSearchLimit)] int limit = CardCatalog.DefaultSearchLimit) =>
                TypedResults.Ok(catalog.Search(search, limit)))
            .WithSummary("Search the card catalogue")
            .WithDescription("Finds cards whose name, type or domain contains the search, ignoring case, best matches first: names that start with it, then other names, then types and domains. For example /api/cards?search=jinx.");

        cards.MapGet("/{id}", Results<Ok<Card>, NotFound<ProblemDetails>> (string id, CardCatalog catalog) =>
                catalog.Find(id) is { } card ? TypedResults.Ok(card) : ApiProblems.NotFound($"No card with id '{id}'."))
            .WithSummary("Get a card");

        return app;
    }
}

/// <summary>
/// The card to feature.
/// </summary>
/// <param name="CardId">The card's id, from GET /api/cards.</param>
public record FeaturedCardChoice([property: Required] string? CardId);
