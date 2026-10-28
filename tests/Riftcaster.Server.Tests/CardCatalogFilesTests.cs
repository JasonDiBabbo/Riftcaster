using Microsoft.Extensions.Logging;

namespace Riftcaster.Server.Tests;

// Each card source's catalog has a file named for it. The one an earlier version called just cards.json was Riftcodex's:
// it's renamed, once, so nothing has to be fetched again.
public sealed class CardCatalogFilesTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("riftcaster-tests-");

    private readonly ListLogger _logger = new();

    public void Dispose() => _directory.Delete(recursive: true);

    private string Path(string name) => System.IO.Path.Combine(_directory.FullName, name);

    [Fact]
    public void TheFilesAreNamedForTheirSources()
    {
        Assert.Equal("cards.riftcodex.json", CardCatalogFiles.Riftcodex);
        Assert.Equal("cards.rifthunt.json", CardCatalogFiles.RiftHunt);
        Assert.Equal("cards.rifthunt-sample.json", CardCatalogFiles.RiftHuntSample);
    }

    [Fact]
    public void TheCatalogAnEarlierVersionSaved_IsRenamedToRiftcodexs()
    {
        File.WriteAllText(Path("cards.json"), "saved cards");

        var path = CardCatalogFiles.RiftcodexPath(_directory.FullName, _logger);

        Assert.Equal(Path("cards.riftcodex.json"), path);
        Assert.False(File.Exists(Path("cards.json")));
        Assert.Equal("saved cards", File.ReadAllText(path)); // Kept as it was, not fetched again
        Assert.Contains(_logger.Records, record => record.Level == LogLevel.Information && record.Message.Contains("cards.riftcodex.json"));
    }

    [Fact]
    public void ARenamedCatalog_IsNotRenamedAgain()
    {
        File.WriteAllText(Path("cards.riftcodex.json"), "current");
        File.WriteAllText(Path("cards.json"), "an old copy");

        var path = CardCatalogFiles.RiftcodexPath(_directory.FullName, _logger);

        Assert.Equal("current", File.ReadAllText(path)); // Not overwritten
        Assert.Equal("an old copy", File.ReadAllText(Path("cards.json"))); // And left alone
        Assert.Empty(_logger.Records);
    }

    [Fact]
    public void WithNoCatalogYet_ThereIsNothingToRename()
    {
        var path = CardCatalogFiles.RiftcodexPath(_directory.FullName, _logger);

        Assert.Equal(Path("cards.riftcodex.json"), path);
        Assert.False(File.Exists(path));
        Assert.Empty(_logger.Records);
    }

    // Something has the old file open: Riftcaster carries on, and fetches Riftcodex's catalog again.
    [Fact]
    public void ACatalogThatCantBeRenamed_IsSaid_NotAnException()
    {
        File.WriteAllText(Path("cards.json"), "saved cards");
        using var holding = new FileStream(Path("cards.json"), FileMode.Open, FileAccess.Read, FileShare.None);

        var path = CardCatalogFiles.RiftcodexPath(_directory.FullName, _logger);

        Assert.Equal(Path("cards.riftcodex.json"), path);
        Assert.Contains(_logger.Records, record => record.Level == LogLevel.Warning && record.Message.Contains("will be fetched again"));
    }

    // Keeps what's logged, for a test to look at.
    private sealed class ListLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Records { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Records.Add((logLevel, formatter(state, exception)));
    }
}
