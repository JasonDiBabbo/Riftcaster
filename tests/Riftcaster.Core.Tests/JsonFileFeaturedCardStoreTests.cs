using Microsoft.Extensions.Logging.Abstractions;
using Riftcaster.Contracts;
using Riftcaster.Core.FeaturedCard;

namespace Riftcaster.Core.Tests;

public sealed class JsonFileFeaturedCardStoreTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("riftcaster-tests-");

    private readonly string _path;

    private readonly JsonFileFeaturedCardStore _store;

    public JsonFileFeaturedCardStoreTests()
    {
        // A subfolder that doesn't exist yet, to check that Save creates it
        _path = Path.Combine(_directory.FullName, "data", "featuredCard.json");
        _store = CreateStore();
    }

    public void Dispose()
    {
        _directory.Delete(recursive: true);
    }

    [Fact]
    public void Load_MissingFile_ReturnsNull()
    {
        Assert.Null(_store.Load());
    }

    public static TheoryData<FeaturedCardState> States() =>
    [
        new FeaturedCardState(new Card("1", "ogn-251-298", "Jinx, Loose Cannon", "Metal", CardType.Legend, null, "Fury / Chaos", null, "Origins", "https://cards.test/1.png", Landscape: false)),
        new FeaturedCardState(Card: null), // Nothing featured: a state of its own, not a missing file
    ];

    [Theory]
    [MemberData(nameof(States))]
    public void SaveThenLoad_RoundTrips(FeaturedCardState state)
    {
        _store.Save(state);

        var loaded = CreateStore().Load();

        Assert.Equal(state, loaded);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{"card":{"id":"1","name":"Jinx"}}""")]
    public void Load_InvalidFile_ReturnsNullAndKeepsBackup(string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, contents);

        var state = _store.Load();

        Assert.Null(state);
        Assert.False(File.Exists(_path));
        Assert.Equal(contents, File.ReadAllText(_path + ".bad"));
    }

    // A fresh store reads from disk, proving the data isn't just held in memory.
    private JsonFileFeaturedCardStore CreateStore() => new(_path, NullLogger<JsonFileFeaturedCardStore>.Instance);
}
