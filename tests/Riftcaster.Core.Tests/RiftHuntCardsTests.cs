using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Riftcaster.Contracts;
using Riftcaster.Core.Cards;

namespace Riftcaster.Core.Tests;

// RiftHunt's bulk card file read into cards: the sample built into Riftcaster, which is 37 of the real file's cards,
// and small files of a test's own for what a card can be missing.
public class RiftHuntCardsTests
{
    private static readonly ILogger<RiftHuntSampleCardSource> Quiet = NullLogger<RiftHuntSampleCardSource>.Instance;

    private static Task<IReadOnlyList<Card>> Sample() => new RiftHuntSampleCardSource(Quiet).FetchAllAsync(null, CancellationToken.None);

    // A one-card file with everything a card needs, which a test changes.
    private static string File(string card) => $$"""{"fetched":"2026-10-19T18:10:54.276Z","source":"riftcodex.com","sets":[],"cards":[{{card}}]}""";

    private static string Item(
        string id = "1",
        string? code = "tst-001-1",
        string? name = "Poppy, Paragon",
        string? type = "Unit",
        string supertype = "null",
        string domains = """["Body"]""",
        string energy = "4",
        string? set = "Origins",
        string? image = "https://cmsassets.rgpub.io/poppy.png",
        string orientation = "portrait",
        string tags = """["Poppy"]""") =>
        $$"""
        {"id":"{{id}}",{{Field("riftboundId", code)}}{{Field("name", name)}}{{Field("type", type)}}"supertype":{{supertype}},"domains":{{domains}},"energy":{{energy}},{{Field("setName", set)}}{{Field("imgUrl", image)}}"orientation":"{{orientation}}","tags":{{tags}}}
        """;

    private static string Field(string name, string? value) => value is null ? "" : $"\"{name}\":\"{value}\",";

    private static async Task<IReadOnlyList<Card>> Read(string json, ILogger<RiftHuntCardSource>? logger = null)
    {
        var history = new InMemoryCardFetchHistory();
        var http = new HttpClient(new Answers(json)) { BaseAddress = new Uri("https://api.rifthunt.com/") };
        return await new RiftHuntCardSource(http, history, TimeProvider.System, logger ?? NullLogger<RiftHuntCardSource>.Instance).FetchAllAsync(null, CancellationToken.None);
    }

    // So the web interface can say it's a stand-in, and dim what would download nothing.
    [Fact]
    public void TheSample_SaysItIsASample_AndTheRealSourceDoesNot()
    {
        ICardSource sample = new RiftHuntSampleCardSource(Quiet);
        ICardSource real = new RiftHuntCardSource(new HttpClient(new ThrowingHandler()), new InMemoryCardFetchHistory(), TimeProvider.System, NullLogger<RiftHuntCardSource>.Instance);

        Assert.True(sample.IsSample);
        Assert.False(real.IsSample);
        Assert.Null(sample.NotBefore);
    }

    [Fact]
    public void TheSampleFile_IsTheOneTheSourceReads()
    {
        var json = RiftHuntSampleCardSource.SampleJson();

        Assert.Contains("\"cards\"", json);
        Assert.Contains("Kai'Sa, Daughter of the Void", json);
    }

    [Fact]
    public async Task TheSample_HasTheCardsWithCardArt_AndNotTheThreeWithAProductPhoto()
    {
        var cards = await Sample();

        Assert.Equal(34, cards.Count); // 37 in the file; the three promos with a TCGplayer photo are skipped
        Assert.DoesNotContain(cards, card => card.Name is "Svellsongur" or "Blue Sentinel");
        Assert.DoesNotContain(cards, card => card.ImageUrl.Contains("tcgplayer"));
    }

    [Fact]
    public async Task ACard_IsReadAsRiftcodexsAre()
    {
        var cards = await Sample();

        var legend = cards.Single(card => card.Code == "ogn-247-298");
        Assert.Equal("Kai'Sa, Daughter of the Void", legend.Name);
        Assert.Null(legend.Variant);
        Assert.Equal(CardType.Legend, legend.Type);
        Assert.Equal(CardSupertype.Champion, legend.Supertype);
        Assert.Equal("Fury / Mind", legend.Domain); // Two domains, joined as Riftcodex's are
        Assert.Null(legend.Energy);
        Assert.Equal("Origins", legend.Set);
        Assert.StartsWith("https://cmsassets.rgpub.io/", legend.ImageUrl);
        Assert.False(legend.Landscape);
        Assert.False(string.IsNullOrEmpty(legend.Id));
    }

    [Fact]
    public async Task AChampionUnit_HasItsSupertypeAndEnergy()
    {
        var jinx = (await Sample()).Single(card => card.Code == "ogn-030-298");

        Assert.Equal(CardType.Unit, jinx.Type);
        Assert.Equal(CardSupertype.Champion, jinx.Supertype);
        Assert.Equal(3, jinx.Energy);
        Assert.Equal("Fury", jinx.Domain);
    }

    // RiftHunt writes the variant after the name, as Riftcodex does: it's split off the same way.
    [Theory]
    [InlineData("ogn-030a-298", "Jinx, Demolitionist", "Alternate Art")]
    [InlineData("ogn-299*-298", "Kai'Sa, Daughter of the Void", "Signature")]
    [InlineData("ogn-299-298", "Kai'Sa, Daughter of the Void", "Overnumbered")]
    [InlineData("opp-181-221", "Rumble, Mechanized Menace", "Metal")]
    public async Task AVariant_IsSplitOffTheName(string code, string name, string variant)
    {
        var card = (await Sample()).Single(card => card.Code == code);

        Assert.Equal(name, card.Name);
        Assert.Equal(variant, card.Variant);
    }

    [Fact]
    public async Task ABattlefield_IsLandscape()
    {
        var cards = await Sample();

        Assert.All(cards.Where(card => card.Type == CardType.Battlefield), card => Assert.True(card.Landscape));
        Assert.All(cards.Where(card => card.Type != CardType.Battlefield), card => Assert.False(card.Landscape));
    }

    [Fact]
    public async Task EachSupertype_IsKnown()
    {
        var cards = await Sample();

        Assert.Contains(cards, card => card.Supertype == CardSupertype.Basic && card.Name == "Fury Rune");
        Assert.Contains(cards, card => card.Supertype == CardSupertype.Token && card.Name.StartsWith("Recruit"));
        Assert.Contains(cards, card => card.Supertype == CardSupertype.Signature && card.Name == "Icathian Rain");
        Assert.DoesNotContain(cards, card => card.Supertype == CardSupertype.Other || card.Type == CardType.Other);
    }

    [Fact]
    public async Task RiftHuntsOwnCardImages_AreCardArtToo()
    {
        var bomb = (await Sample()).Single(card => card.Name == "Bomb");

        Assert.StartsWith("https://rifthunt.com/", bomb.ImageUrl);
    }

    [Fact]
    public async Task TheLegendsChampionsAndBattlefields_CanFillThePlayersLists()
    {
        var catalog = new CardCatalog(new InMemoryCardStore(), new InMemoryCardSource { Cards = await Sample() }, TimeProvider.System, NullLogger<CardCatalog>.Instance);
        await catalog.RefreshAsync(CancellationToken.None);

        Assert.NotEmpty(catalog.Legends);
        Assert.Contains(catalog.ChampionUnits, card => card.Name == "Jinx, Demolitionist");
        Assert.Equal(["Altar to Unity", "Aspirant's Climb", "Back-Alley Bar"], catalog.Battlefields.Select(card => card.Name));
    }

    [Fact]
    public async Task ACardWithoutImageOrAnything_IsSkippedAndSaid()
    {
        var logger = new FakeLogger<RiftHuntCardSource>();
        var json = File($"{Item()},{Item(id: "2", image: null)},{Item(id: "3", code: null)},{Item(id: "4", name: null)},{Item(id: "5", set: null)},null");

        var cards = await Read(json, logger);

        Assert.Equal(["1"], cards.Select(card => card.Id));
        Assert.Contains(logger.Collector.GetSnapshot(), record => record.Level == LogLevel.Warning && record.Message.Contains("Skipped 5 RiftHunt cards"));
    }

    [Theory]
    [InlineData("https://tcgplayer-cdn.tcgplayer.com/product/1_in_1000x1000.jpg")]
    [InlineData("http://cmsassets.rgpub.io/poppy.png")] // Not https
    [InlineData("https://evil.example/cmsassets.rgpub.io/poppy.png")]
    [InlineData("https://notrifthunt.com/img/cards/poppy.webp")]
    [InlineData("not an address")]
    public async Task AnImageThatIsNotCardArt_SkipsTheCard(string image)
    {
        var cards = await Read(File($"{Item()},{Item(id: "2", image: image)}"));

        Assert.Equal(["1"], cards.Select(card => card.Id));
    }

    [Theory]
    [InlineData("https://cmsassets.rgpub.io/poppy.png")]
    [InlineData("https://rifthunt.com/img/cards/rad-t02.webp")]
    [InlineData("https://images.rifthunt.com/cards/poppy.webp")]
    public async Task CardArt_IsKept(string image)
    {
        var cards = await Read(File(Item(image: image)));

        Assert.Equal([image], cards.Select(card => card.ImageUrl));
    }

    [Fact]
    public async Task ATypeNobodyKnows_IsKeptAsOther_AndSaid()
    {
        var logger = new FakeLogger<RiftHuntCardSource>();

        var cards = await Read(File(Item(type: "Artifact", supertype: "\"Mythic\"")), logger);

        var card = Assert.Single(cards);
        Assert.Equal(CardType.Other, card.Type);
        Assert.Equal(CardSupertype.Other, card.Supertype);
        Assert.Contains(logger.Collector.GetSnapshot(), record => record.Message.Contains("Artifact") && record.Message.Contains("Mythic"));
    }

    [Fact]
    public async Task ACardWithNoDomains_HasAnEmptyDomain()
    {
        var card = Assert.Single(await Read(File(Item(domains: "[]"))));

        Assert.Equal("", card.Domain);
    }

    [Theory]
    [InlineData("""{"fetched":"2026-10-19T18:10:54.276Z"}""")]
    [InlineData("[]")]
    [InlineData("not json")]
    public async Task SomethingThatIsNotTheCardFile_IsAJsonException(string json)
    {
        await Assert.ThrowsAnyAsync<JsonException>(() => Read(json));
    }

    // An empty list is a card file. The catalog treats the empty catalog as a failed fetch and keeps what it has.
    [Fact]
    public async Task AFileWithNoCards_IsReadAsNoCards()
    {
        Assert.Empty(await Read("""{"cards":[]}"""));
    }
}
