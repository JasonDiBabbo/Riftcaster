using Microsoft.Extensions.Logging.Abstractions;
using Riftcaster.Contracts;
using Riftcaster.Core.Cards;

namespace Riftcaster.Core.Tests;

public sealed class JsonFileCardStoreTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("riftcaster-tests-");

    private readonly string _path;

    private readonly JsonFileCardStore _store;

    public JsonFileCardStoreTests()
    {
        // A subfolder that doesn't exist yet, to check that Save creates it
        _path = Path.Combine(_directory.FullName, "data", "cards.json");
        _store = CreateStore();
    }

    public void Dispose()
    {
        _directory.Delete(recursive: true);
    }

    [Fact]
    public void Load_MissingFile_ReturnsNull()
    {
        var snapshot = _store.Load();

        Assert.Null(snapshot);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        Card[] cards =
        [
            new("1", "unl-229*-219", "Vi, Piltover Enforcer", "Signature", CardType.Legend, null, "Fury / Order", null, "Unleashed", "https://cards.test/vi.png", Landscape: false),
            new("2", "unl-215-219", "Star Spring", null, CardType.Battlefield, null, "Colorless", null, "Unleashed", "https://cards.test/star.png", Landscape: true),
            new("3", "unl-116a-219", "Poppy, Paragon", "Alternate Art", CardType.Unit, CardSupertype.Champion, "Body", 5, "Unleashed", "https://cards.test/poppy.png", Landscape: false),
        ];
        var fetchedAt = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        _store.Save(new CardCatalogSnapshot(cards, fetchedAt));

        var loaded = CreateStore().Load();

        Assert.NotNull(loaded);
        Assert.Equal(cards, loaded.Cards);
        Assert.Equal(fetchedAt, loaded.FetchedAt);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{"cards":null,"fetchedAt":"2026-10-02T12:00:00+00:00"}""")]
    [InlineData("""{"cards":[{"id":"1","code":"x","name":"X","variant":null,"type":"Planet","supertype":null,"domain":"Fury","energy":null,"set":"S","imageUrl":"https://x.png","landscape":false}],"fetchedAt":"2026-10-02T12:00:00+00:00"}""")]
    public void Load_InvalidFile_ReturnsNullAndKeepsBackup(string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, contents);

        var snapshot = _store.Load();

        Assert.Null(snapshot);
        Assert.False(File.Exists(_path));
        Assert.Equal(contents, File.ReadAllText(_path + ".bad"));
    }

    // A fresh store reads from disk, proving the data isn't just held in memory.
    private JsonFileCardStore CreateStore() => new(_path, NullLogger<JsonFileCardStore>.Instance);
}
