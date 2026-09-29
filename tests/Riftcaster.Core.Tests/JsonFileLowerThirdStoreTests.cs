using Microsoft.Extensions.Logging.Abstractions;
using Riftcaster.Contracts;
using Riftcaster.Core.LowerThird;

namespace Riftcaster.Core.Tests;

public sealed class JsonFileLowerThirdStoreTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("riftcaster-tests-");

    private readonly string _path;

    private readonly JsonFileLowerThirdStore _store;

    public JsonFileLowerThirdStoreTests()
    {
        // A subfolder that doesn't exist yet, to check that Save creates it
        _path = Path.Combine(_directory.FullName, "data", "lowerThird.json");
        _store = CreateStore();
    }

    public void Dispose()
    {
        _directory.Delete(recursive: true);
    }

    [Fact]
    public void Load_MissingFile_ReturnsEmpty()
    {
        var entries = _store.Load();

        Assert.Empty(entries);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var newer = new LowerThirdEntry(Guid.NewGuid(), new LowerThirdKeywordMessage("Burn", "Send cards to the trash."));
        var older = new LowerThirdEntry(Guid.NewGuid(), new LowerThirdKeywordMessage("Draw", "Draw a card from your deck."));
        _store.Save([newer, older]);

        var loaded = CreateStore().Load();

        Assert.Equal([newer, older], loaded);
    }

    [Fact]
    public void Save_LeavesNoTempFile()
    {
        _store.Save([]);

        Assert.True(File.Exists(_path));
        Assert.False(File.Exists(_path + ".tmp"));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{"entries":[{"id":"3fa85f64-5717-4562-b3fc-2c963f66afa6","message":{"type":"unknown"}}]}""")]
    [InlineData("""{"entries":[{"id":"3fa85f64-5717-4562-b3fc-2c963f66afa6","message":{"keyword":"Burn"}}]}""")]
    public void Load_InvalidFile_ReturnsEmptyAndKeepsBackup(string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, contents);

        var entries = _store.Load();

        Assert.Empty(entries);
        Assert.False(File.Exists(_path));
        Assert.Equal(contents, File.ReadAllText(_path + ".bad"));
    }

    // A fresh store reads from disk, proving the data isn't just held in memory.
    private JsonFileLowerThirdStore CreateStore() => new(_path, NullLogger<JsonFileLowerThirdStore>.Instance);
}
