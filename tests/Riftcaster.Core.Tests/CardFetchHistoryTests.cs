using Microsoft.Extensions.Logging.Abstractions;
using Riftcaster.Contracts;
using Riftcaster.Core.Cards;
using Riftcaster.Core.Settings;

namespace Riftcaster.Core.Tests;

// What a card source remembers about its fetches, kept in memory or in a file, and the card source chosen on the Settings page.
public sealed class CardFetchHistoryTests : IDisposable
{
    private static readonly CardFetchRecord Day = new(new DateTimeOffset(2026, 10, 25, 9, 42, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 26, 9, 42, 0, TimeSpan.Zero), 2);

    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("riftcaster-tests-");

    private string File => Path.Combine(_directory.FullName, "data", "cardSources.json");

    private JsonFileCardFetchHistory Create() => new(File, NullLogger<JsonFileCardFetchHistory>.Instance);

    public void Dispose() => _directory.Delete(recursive: true);

    [Fact]
    public void InMemory_RemembersEachSourceApart()
    {
        var history = new InMemoryCardFetchHistory();

        history.Save(CardSourceKind.RiftHunt, Day);

        Assert.Equal(Day, history.Get(CardSourceKind.RiftHunt));
        Assert.Equal(CardFetchRecord.None, history.Get(CardSourceKind.Riftcodex));
    }

    [Fact]
    public void NothingRemembered_IsNone()
    {
        Assert.Null(CardFetchRecord.None.LastAttemptAt);
        Assert.Null(CardFetchRecord.None.NotBefore);
        Assert.Equal(0, CardFetchRecord.None.Failures);
        Assert.Equal(CardFetchRecord.None, Create().Get(CardSourceKind.RiftHunt));
    }

    [Fact]
    public void AFile_RoundTripsAcrossInstances_AndCreatesItsFolder()
    {
        Create().Save(CardSourceKind.RiftHunt, Day);

        Assert.True(System.IO.File.Exists(File));
        Assert.Equal(Day, Create().Get(CardSourceKind.RiftHunt));
        Assert.Equal(CardFetchRecord.None, Create().Get(CardSourceKind.Riftcodex));
    }

    [Fact]
    public void ASaveKeepsTheOtherSources()
    {
        var other = Day with { Failures = 0 };
        var history = Create();
        history.Save(CardSourceKind.Riftcodex, other);
        history.Save(CardSourceKind.RiftHunt, Day);

        var again = Create();
        Assert.Equal(other, again.Get(CardSourceKind.Riftcodex));
        Assert.Equal(Day, again.Get(CardSourceKind.RiftHunt));
    }

    // Saved by a later version with a source this one doesn't have: left out, not a failure to read the rest.
    [Fact]
    public void ASourceThisVersionDoesNotKnow_IsLeftOut()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File)!);
        System.IO.File.WriteAllText(File, """{"sources":{"Gizmo":{"lastAttemptAt":null,"notBefore":null,"failures":9},"RiftHunt":{"lastAttemptAt":null,"notBefore":null,"failures":1}}}""");

        var history = Create();

        Assert.Equal(1, history.Get(CardSourceKind.RiftHunt).Failures);
    }

    [Fact]
    public void AFileThatIsNotJson_IsNoHistory_AndIsMovedAside()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File)!);
        System.IO.File.WriteAllText(File, "not json");

        var history = Create();

        Assert.Equal(CardFetchRecord.None, history.Get(CardSourceKind.RiftHunt));
        Assert.True(System.IO.File.Exists(File + ".bad"));
    }

    [Fact]
    public void AnUnwritableFile_IsNotAnException()
    {
        // The file's name is a folder that exists, so it can't be written.
        Directory.CreateDirectory(File);

        Create().Save(CardSourceKind.RiftHunt, Day);
    }

    [Fact]
    public void TheCardSourceChosen_IsRiftcodexUntilAnotherIs_AndSaved()
    {
        var store = new InMemoryAppSettingsStore();
        var settings = new AppSettingsService(store);
        Assert.Equal(CardSourceKind.Riftcodex, settings.Settings.Cards.Source);

        settings.ChooseCardSource(CardSourceKind.RiftHunt);

        Assert.Equal(CardSourceKind.RiftHunt, settings.Settings.Cards.Source);
        Assert.Equal(CardSourceKind.RiftHunt, store.Settings!.Cards.Source);
    }

    // The JSON accepts numbers for an enum, so a hand-edited file can hold one that isn't a source.
    [Fact]
    public void ACardSourceNobodyKnows_IsRiftcodex()
    {
        var store = new InMemoryAppSettingsStore();
        store.Save(new AppSettings { Cards = new CardSettings { Source = (CardSourceKind)9 } });

        Assert.Equal(CardSourceKind.Riftcodex, new AppSettingsService(store).Settings.Cards.Source);
    }

    [Fact]
    public void ASettingsFileFromBeforeTheCatalogSource_StartsOnRiftcodex()
    {
        var store = new InMemoryAppSettingsStore();
        store.Save(new AppSettings());

        Assert.Equal(CardSourceKind.Riftcodex, new AppSettingsService(store).Settings.Cards.Source);
    }
}
