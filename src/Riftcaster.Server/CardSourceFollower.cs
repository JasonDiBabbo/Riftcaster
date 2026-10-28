using Riftcaster.Core.Cards;
using Riftcaster.Core.FeaturedCard;
using Riftcaster.Core.Players;
using Riftcaster.Core.Settings;

namespace Riftcaster.Server;

/// <summary>
/// Keeps the rest of Riftcaster in step when the card catalog moves to another source: the source is remembered in
/// the settings, so it's the one in use from the next start, and the cards the players and the Featured card overlay
/// have chosen are swapped for the same cards from the new catalog, by name.
/// </summary>
/// <remarks>
/// A choice is a copy of a card as it was chosen, with the old source's id. Left alone it would still show, but as a
/// card the new catalog doesn't know, next to the same card with its own id in the lists. One the new catalog has no
/// card of that name for stays as it was.
/// </remarks>
/// <param name="catalog">The card catalog.</param>
/// <param name="settings">Where the source in use is remembered.</param>
/// <param name="players">The players, whose legends, champions and battlefields are cards.</param>
/// <param name="featuredCard">The Featured card overlay's card.</param>
/// <param name="logger">Logs the swap.</param>
internal sealed class CardSourceFollower(
    CardCatalog catalog,
    AppSettingsService settings,
    PlayersService players,
    FeaturedCardService featuredCard,
    ILogger<CardSourceFollower> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        catalog.SourceChanged += OnSourceChanged;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        catalog.SourceChanged -= OnSourceChanged;
        return Task.CompletedTask;
    }

    private void OnSourceChanged()
    {
        settings.ChooseCardSource(catalog.ActiveSource);
        players.ReplaceCards(catalog.FindEquivalent);
        featuredCard.ReplaceCard(catalog.FindEquivalent);
        logger.LogInformation("The cards chosen for the players and the Featured card overlay now come from the {Source} catalog, where it has them.", catalog.ActiveSource);
    }
}
