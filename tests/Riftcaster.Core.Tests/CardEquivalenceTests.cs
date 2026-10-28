using Microsoft.Extensions.Logging.Abstractions;
using Riftcaster.Contracts;
using Riftcaster.Core.Cards;
using Riftcaster.Core.FeaturedCard;
using Riftcaster.Core.Match;
using Riftcaster.Core.Players;

namespace Riftcaster.Core.Tests;

// The same card in another source has another id: what's chosen for the players and the Featured card overlay is a copy
// with the old source's id, which is swapped for the new catalog's by name when the source changes.
public class CardEquivalenceTests
{
    // Riftcodex's copies, and RiftHunt's, of the same cards (the ids differ, nothing else needs to).
    private static readonly Card JinxOld = NewCard("c1", "ogn-251-298", "Jinx, Loose Cannon", null, CardType.Legend);
    private static readonly Card PoppyOld = NewCard("c2", "ogn-001-298", "Poppy, Paragon", null, CardType.Unit);
    private static readonly Card AltarOld = NewCard("c3", "ogn-275-298", "Altar to Unity", null, CardType.Battlefield);
    private static readonly Card JinxAltOld = NewCard("c4", "ogn-030a-298", "Jinx, Demolitionist", "Alternate Art", CardType.Unit);

    private static readonly Card Jinx = NewCard("h1", "ogn-251-298", "Jinx, Loose Cannon", null, CardType.Legend);
    private static readonly Card Poppy = NewCard("h2", "ogn-001-298", "Poppy, Paragon", null, CardType.Unit);
    private static readonly Card Altar = NewCard("h3", "ogn-275-298", "Altar to Unity", null, CardType.Battlefield);
    private static readonly Card JinxStandard = NewCard("h4", "ogn-030-298", "Jinx, Demolitionist", null, CardType.Unit);
    private static readonly Card JinxAlt = NewCard("h5", "ogn-030a-298", "Jinx, Demolitionist", "Alternate Art", CardType.Unit);
    private static readonly Card JinxMetal = NewCard("h6", "ogn-251-298", "Jinx, Loose Cannon", "Metal", CardType.Legend);

    private static Card NewCard(string id, string code, string name, string? variant, CardType type) =>
        new(id, code, name, variant, type, null, "Fury", Energy: null, "Origins", $"https://cards.test/{id}.png", Landscape: false);

    private static CardCatalog CatalogOf(params Card[] cards)
    {
        var store = new InMemoryCardStore();
        store.Save(new CardCatalogSnapshot(cards, DateTimeOffset.UnixEpoch));
        return new CardCatalog(store, new InMemoryCardSource(), TimeProvider.System, NullLogger<CardCatalog>.Instance);
    }

    [Fact]
    public void ACardIsFoundByNameInTheOtherCatalog()
    {
        var catalog = CatalogOf(Jinx, Poppy, Altar);

        Assert.Equal(Jinx, catalog.FindEquivalent(JinxOld));
        Assert.Equal(Altar, catalog.FindEquivalent(AltarOld));
    }

    [Fact]
    public void TheSameVariantIsPreferred()
    {
        var catalog = CatalogOf(JinxStandard, JinxAlt);

        Assert.Equal(JinxAlt, catalog.FindEquivalent(JinxAltOld));
    }

    [Fact]
    public void WithoutThatVariant_TheStandardPrintingOfTheNameIsTheNext()
    {
        var catalog = CatalogOf(JinxStandard);

        Assert.Equal(JinxStandard, catalog.FindEquivalent(JinxAltOld));
    }

    [Fact]
    public void WithoutThatVariantOrAStandardPrinting_ThereIsNoMatch()
    {
        var catalog = CatalogOf(JinxMetal);

        Assert.Null(catalog.FindEquivalent(JinxOld)); // Only a metal one: not the card that was chosen
    }

    [Fact]
    public void TheSameCollectorCodeBreaksATieBetweenCardsOfOneNameAndVariant()
    {
        var other = NewCard("h9", "ogn-999-298", "Jinx, Loose Cannon", null, CardType.Legend);
        var catalog = CatalogOf(other, Jinx);

        Assert.Equal(Jinx, catalog.FindEquivalent(JinxOld));
    }

    [Fact]
    public void NamesAreComparedIgnoringCase()
    {
        var catalog = CatalogOf(NewCard("h7", "ogn-251-298", "JINX, LOOSE CANNON", null, CardType.Legend));

        Assert.NotNull(catalog.FindEquivalent(JinxOld));
    }

    [Fact]
    public void ACardTheOtherCatalogLacks_IsNotFound()
    {
        Assert.Null(CatalogOf(Poppy).FindEquivalent(JinxOld));
        Assert.Null(CatalogOf().FindEquivalent(JinxOld));
    }

    [Fact]
    public void ThePlayersCardsAreSwapped_AndOnesWithoutAMatchStay()
    {
        var store = new InMemoryPlayersStore();
        var service = new PlayersService(store, new MatchService(new InMemoryMatchStore()));
        service.UpdatePlayer(0, player => player with { Name = "Mara", Legend = JinxOld, Champion = PoppyOld, Battlefield = AltarOld });
        service.UpdatePlayer(1, player => player with { Legend = JinxAltOld }); // Not a legend, but any card will do
        var catalog = CatalogOf(Jinx, Altar);
        var changes = 0;
        service.Changed += () => changes++;

        service.ReplaceCards(catalog.FindEquivalent);

        var mara = service.State.Players[0];
        Assert.Equal(Jinx, mara.Legend);
        Assert.Equal(PoppyOld, mara.Champion); // The new catalog has no Poppy: it stays as it was
        Assert.Equal(Altar, mara.Battlefield);
        Assert.Equal("Mara", mara.Name); // Nothing else changes
        Assert.Equal(JinxAltOld, service.State.Players[1].Legend); // No Jinx, Demolitionist in the new catalog either
        Assert.Equal(1, changes);
        Assert.Equal(Jinx, store.State!.Players[0].Legend); // And it's saved
    }

    [Fact]
    public void ThePlayersWithNoCards_AreLeftAlone_AndNothingIsSaidChanged()
    {
        var service = new PlayersService(new InMemoryPlayersStore(), new MatchService(new InMemoryMatchStore()));
        var changes = 0;
        service.Changed += () => changes++;

        service.ReplaceCards(CatalogOf(Jinx).FindEquivalent);

        Assert.Equal(0, changes);
    }

    [Fact]
    public void CardsAlreadyFromTheNewCatalog_AreNotChanged()
    {
        var service = new PlayersService(new InMemoryPlayersStore(), new MatchService(new InMemoryMatchStore()));
        service.UpdatePlayer(0, player => player with { Legend = Jinx });
        var changes = 0;
        service.Changed += () => changes++;

        service.ReplaceCards(CatalogOf(Jinx).FindEquivalent);

        Assert.Equal(0, changes);
    }

    [Fact]
    public void ReplacingPlayersCardsNeedsAWayToFindThem()
    {
        var service = new PlayersService(new InMemoryPlayersStore(), new MatchService(new InMemoryMatchStore()));

        Assert.Throws<ArgumentNullException>(() => service.ReplaceCards(null!));
    }

    [Fact]
    public void TheFeaturedCardIsSwapped()
    {
        var store = new InMemoryFeaturedCardStore();
        store.Save(new FeaturedCardState(JinxOld));
        var catalog = CatalogOf(Jinx);
        var service = new FeaturedCardService(store, catalog);
        var changes = 0;
        service.Changed += () => changes++;

        service.ReplaceCard(catalog.FindEquivalent);

        Assert.Equal(Jinx, service.Current);
        Assert.Equal(new FeaturedCardState(Jinx), store.State);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void AFeaturedCardWithoutAMatch_StaysOnAir()
    {
        var store = new InMemoryFeaturedCardStore();
        store.Save(new FeaturedCardState(PoppyOld));
        var catalog = CatalogOf(Jinx);
        var service = new FeaturedCardService(store, catalog);
        var changes = 0;
        service.Changed += () => changes++;

        service.ReplaceCard(catalog.FindEquivalent);

        Assert.Equal(PoppyOld, service.Current);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void NothingFeatured_NothingIsSwapped()
    {
        var catalog = CatalogOf(Jinx);
        var service = new FeaturedCardService(new InMemoryFeaturedCardStore(), catalog);

        service.ReplaceCard(catalog.FindEquivalent);

        Assert.Null(service.Current);
    }

    [Fact]
    public void AFeaturedCardAlreadyFromTheCatalog_IsNotSaidChanged()
    {
        var store = new InMemoryFeaturedCardStore();
        store.Save(new FeaturedCardState(Jinx));
        var catalog = CatalogOf(Jinx);
        var service = new FeaturedCardService(store, catalog);
        var changes = 0;
        service.Changed += () => changes++;

        service.ReplaceCard(catalog.FindEquivalent);

        Assert.Equal(0, changes);
    }

    [Fact]
    public void ReplacingTheFeaturedCardNeedsAWayToFindIt()
    {
        var service = new FeaturedCardService(new InMemoryFeaturedCardStore(), CatalogOf());

        Assert.Throws<ArgumentNullException>(() => service.ReplaceCard(null!));
    }
}
