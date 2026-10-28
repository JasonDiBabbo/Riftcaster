using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Riftcaster.Contracts;
using Riftcaster.Core.Cards;
using Riftcaster.WebUI.Settings;

namespace Riftcaster.WebUI.Tests;

// The Settings page's card catalog section: which source the catalog is from, switching to the other (at once if it's saved,
// else with a download the catalog stays as it was during), and fetching again.
public class CardCatalogSectionTests : BunitContext
{
    private static readonly Card Jinx = NewCard("1", "Jinx, Loose Cannon");
    private static readonly Card Poppy = NewCard("2", "Poppy, Paragon");
    private static readonly Card Recruit = NewCard("3", "Recruit");
    private static readonly Card HuntJinx = NewCard("h1", "Jinx, Loose Cannon");

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero));

    private readonly FakeSource _codex = new() { Cards = [Jinx, Poppy, Recruit] };

    private readonly FakeSource _hunt = new() { Cards = [HuntJinx] };

    private readonly InMemoryCardStore _codexStore = new();

    private readonly InMemoryCardStore _huntStore = new();

    private CardCatalog _catalog = null!;

    private static Card NewCard(string id, string name) =>
        new(id, $"tst-{id}", name, null, CardType.Unit, null, "Fury", Energy: null, "Test Set", $"https://cards.test/{id}.png", Landscape: false);

    private IRenderedComponent<CardCatalogSection> RenderSection(CardSourceKind active = CardSourceKind.Riftcodex, bool codexSaved = true)
    {
        if (codexSaved)
        {
            _codexStore.Save(new CardCatalogSnapshot([Jinx, Poppy, Recruit], _time.GetUtcNow() - TimeSpan.FromHours(3)));
        }

        _catalog = new CardCatalog(
            new CardSourceSet((CardSourceKind.Riftcodex, _codex, _codexStore), (CardSourceKind.RiftHunt, _hunt, _huntStore)),
            active,
            _time,
            NullLogger<CardCatalog>.Instance);
        Services.AddSingleton(_catalog);
        Services.AddSingleton<TimeProvider>(_time);
        return Render<CardCatalogSection>();
    }

    private const string ContinuesWorking = "Previously downloaded cards continue working while new downloads process.";

    // The saved catalog's facts as the section says them: how big it is, and how long ago (and when, in which time zone) it was fetched.
    private List<string> Facts(int cards, TimeSpan fetchedAgo, string ago) =>
    [
        $"Catalog size: {cards} card{(cards == 1 ? "" : "s")}",
        $"Last fetched: {ago} ({SettingsLabels.Timestamp(_time.GetUtcNow() - fetchedAgo)})",
    ];

    private static List<string> FactsOf(IRenderedComponent<CardCatalogSection> section) =>
        [.. section.FindAll(".facts li").Select(item => item.TextContent)];

    private static AngleSharp.Dom.IElement Segment(IRenderedComponent<CardCatalogSection> section, string label) =>
        section.Find(".segmented[aria-label='Catalog source']").QuerySelectorAll("button").Single(button => button.TextContent.Trim() == label);

    private static string Selected(IRenderedComponent<CardCatalogSection> section) =>
        section.Find(".segmented[aria-label='Catalog source'] button[aria-pressed='true']").TextContent.Trim();

    private static string Row(IRenderedComponent<CardCatalogSection> section, string title)
    {
        // The phone's copy of the note sits inside the description: it's not the description.
        var description = section.FindAll(".row").Single(row => row.QuerySelector(".title")!.TextContent.Trim() == title).QuerySelector(".description")!;
        description.QuerySelector(".note-inline")?.Remove();
        return description.TextContent;
    }

    [Fact]
    public void ShowsTheSourceInUse_WhatToKnowAboutIt_AndTheSavedCatalog()
    {
        var section = RenderSection();

        Assert.Equal("Card catalog", section.Find("h2").TextContent);
        Assert.Empty(section.FindAll(".meta")); // No section has subtext beside its heading
        Assert.Equal(["Riftcodex", "RiftHunt"], section.Find(".segmented").QuerySelectorAll("button").Select(button => button.TextContent.Trim()));
        Assert.Equal("Riftcodex", Selected(section));
        Assert.Contains("Each source keeps its own saved catalog, so switching back is instant", Row(section, "Catalog source"));
        Assert.Equal("Riftcodex is fetched page by page, which can take a few minutes.", section.Find(".hint:not(.sample)").TextContent.Trim());
        Assert.Equal(Facts(3, TimeSpan.FromHours(3), "3 hours ago"), FactsOf(section));
        Assert.False(section.Find("button.refresh").HasAttribute("disabled"));
        Assert.Equal(ContinuesWorking, section.Find(".control .note").TextContent.Trim());
    }

    [Fact]
    public void NoCardsYet_SaysSo()
    {
        _codexStore.Save(new CardCatalogSnapshot([], _time.GetUtcNow()));
        _catalog = new CardCatalog(new CardSourceSet((CardSourceKind.Riftcodex, _codex, new InMemoryCardStore())), CardSourceKind.Riftcodex, _time, NullLogger<CardCatalog>.Instance);
        Services.AddSingleton(_catalog);
        Services.AddSingleton<TimeProvider>(_time);

        var section = Render<CardCatalogSection>();

        Assert.Equal(["No cards yet."], FactsOf(section));
    }

    [Fact]
    public void RiftHuntInUse_ExplainsItsOnceADayLimit_AndHowLongARefreshTakes()
    {
        _huntStore.Save(new CardCatalogSnapshot([HuntJinx], _time.GetUtcNow().AddMinutes(-20)));

        var section = RenderSection(CardSourceKind.RiftHunt);

        Assert.Equal("RiftHunt", Selected(section));
        Assert.Equal("RiftHunt serves their whole catalog as one download. At their request, users will only be allowed to download the card catalog once per day to avoid stress on their service.", section.Find(".hint:not(.sample)").TextContent.Trim());
        Assert.Equal(Facts(1, TimeSpan.FromMinutes(20), "20 minutes ago"), FactsOf(section));
        Assert.Equal(ContinuesWorking, section.Find(".control .note").TextContent.Trim()); // The same under either source
    }

    [Fact]
    public void ASourceThatMayNotBeAskedYet_DisablesRefreshNow_AndSaysWhenItMay()
    {
        _huntStore.Save(new CardCatalogSnapshot([HuntJinx], _time.GetUtcNow()));
        _hunt.NotBefore = _time.GetUtcNow().AddHours(21);

        var section = RenderSection(CardSourceKind.RiftHunt);

        Assert.True(section.Find("button.refresh").HasAttribute("disabled"));
        List<string> expected = [.. Facts(1, TimeSpan.Zero, "just now"), $"Next fetch allowed: {SettingsLabels.When(_time.GetUtcNow().AddHours(21), _time.GetUtcNow())}"];
        Assert.Equal(expected, FactsOf(section));
        Assert.Equal(ContinuesWorking, section.Find(".control .note").TextContent.Trim());
        Assert.StartsWith("Available again", section.Find("button.refresh").GetAttribute("title"));
    }

    [Fact]
    public async Task RefreshNow_FetchesTheCatalogAgain()
    {
        var section = RenderSection();
        _codex.Cards = [Jinx, Poppy, Recruit, NewCard("4", "Star Spring")];

        await section.InvokeAsync(() => section.Find("button.refresh").Click());

        Assert.Equal(1, _codex.FetchCount);
        Assert.Equal(4, _catalog.Cards.Count);
        section.WaitForAssertion(() => Assert.Contains("Catalog size: 4 cards", FactsOf(section)));
    }

    [Fact]
    public async Task WhileTheCatalogIsFetching_RefreshNowIsDisabled_AndTheSavedCatalogSaysHowFarItHasGot()
    {
        var section = RenderSection();
        _codex.Gate = new TaskCompletionSource();
        _codex.ProgressBeforeGate = new CardFetchProgress(4, 15);

        var refreshing = _catalog.RefreshAsync(CancellationToken.None);

        section.WaitForAssertion(() =>
        {
            Assert.Equal(["Updating… page 4 of 15", .. Facts(3, TimeSpan.FromHours(3), "3 hours ago")], FactsOf(section));
            Assert.True(section.Find("button.refresh").HasAttribute("disabled"));
            Assert.Empty(section.FindAll(".box")); // An ordinary refresh isn't a switch
        });
        _codex.Gate.SetResult();
        await refreshing;
    }

    [Fact]
    public async Task ChoosingASourceWithASavedCatalog_SwitchesAtOnce()
    {
        _huntStore.Save(new CardCatalogSnapshot([HuntJinx], _time.GetUtcNow().AddHours(-2)));
        var section = RenderSection();

        await section.InvokeAsync(() => Segment(section, "RiftHunt").Click());

        Assert.Equal(CardSourceKind.RiftHunt, _catalog.ActiveSource);
        Assert.Equal("RiftHunt", Selected(section));
        Assert.Contains("RiftHunt serves their whole catalog as one download", section.Find(".hint:not(.sample)").TextContent);
        Assert.Equal(Facts(1, TimeSpan.FromHours(2), "2 hours ago"), FactsOf(section));
        Assert.Empty(section.FindAll(".box"));
        Assert.Equal(0, _hunt.FetchCount);
    }

    [Fact]
    public async Task ChoosingASourceToDownload_ShowsItDownloading_WithTheCatalogInUseUnchanged()
    {
        var section = RenderSection();
        _hunt.Gate = new TaskCompletionSource();

        var switching = Task.Run(() => section.InvokeAsync(() => Segment(section, "RiftHunt").Click()));

        section.WaitForAssertion(() =>
        {
            Assert.Equal("Downloading the RiftHunt catalog", section.Find(".box.working b").TextContent);
            Assert.Contains("Still using Riftcodex (3 cards) until this finishes. Nothing changes on air.", section.Find(".box.working").TextContent);
            Assert.Equal("RiftHunt", Selected(section)); // What was asked for
            Assert.Equal(Facts(3, TimeSpan.FromHours(3), "3 hours ago"), FactsOf(section)); // The catalog in use
        });
        Assert.Equal(CardSourceKind.Riftcodex, _catalog.ActiveSource);

        _hunt.Gate.SetResult();
        await switching;
        section.WaitForAssertion(() =>
        {
            Assert.Empty(section.FindAll(".box"));
            Assert.Equal(Facts(1, TimeSpan.Zero, "just now"), FactsOf(section));
        });
    }

    // Riftcodex is fetched page by page, and says which, when it's the one being switched to.
    [Fact]
    public async Task ADownloadByPages_SaysWhichPage()
    {
        _huntStore.Save(new CardCatalogSnapshot([HuntJinx], _time.GetUtcNow()));
        var section = RenderSection(CardSourceKind.RiftHunt, codexSaved: false);
        _codex.Gate = new TaskCompletionSource();
        _codex.ProgressBeforeGate = new CardFetchProgress(4, 15);

        var switching = Task.Run(() => section.InvokeAsync(() => Segment(section, "Riftcodex").Click()));

        section.WaitForAssertion(() =>
        {
            Assert.Equal("Downloading the Riftcodex catalog", section.Find(".box.working b").TextContent);
            Assert.Contains("Still using RiftHunt (1 card) until this finishes. Nothing changes on air. Page 4 of 15.", section.Find(".box.working").TextContent);
        });
        _codex.Gate.SetResult();
        await switching;
    }

    [Fact]
    public async Task CancelingADownload_LeavesTheCatalogAsItWas()
    {
        var section = RenderSection();
        _hunt.Gate = new TaskCompletionSource();
        var switching = Task.Run(() => section.InvokeAsync(() => Segment(section, "RiftHunt").Click()));
        section.WaitForAssertion(() => Assert.NotEmpty(section.FindAll(".box.working")));

        section.Find(".box.working button").Click();
        await switching;

        section.WaitForAssertion(() => Assert.Empty(section.FindAll(".box")));
        Assert.Equal(CardSourceKind.Riftcodex, _catalog.ActiveSource);
        Assert.Equal("Riftcodex", Selected(section));
    }

    // Asking for the source in use again, while the other downloads, is taking it back.
    [Fact]
    public async Task ChoosingTheSourceInUseWhileAnotherDownloads_CancelsTheDownload()
    {
        var section = RenderSection();
        _hunt.Gate = new TaskCompletionSource();
        var switching = Task.Run(() => section.InvokeAsync(() => Segment(section, "RiftHunt").Click()));
        section.WaitForAssertion(() => Assert.NotEmpty(section.FindAll(".box.working")));

        Segment(section, "Riftcodex").Click();
        await switching;

        section.WaitForAssertion(() => Assert.Empty(section.FindAll(".box")));
        Assert.Equal(CardSourceKind.Riftcodex, _catalog.ActiveSource);
    }

    [Fact]
    public async Task AFailedDownload_SaysSo_AndThatTheSourceIsStillTheOneInUse()
    {
        var section = RenderSection();
        _hunt.Failure = new HttpRequestException("No such host is known.");

        await section.InvokeAsync(() => Segment(section, "RiftHunt").Click());

        var box = section.Find(".box.problem");
        Assert.Equal("Couldn't reach RiftHunt", box.QuerySelector("b")!.TextContent);
        Assert.Contains("The source is still Riftcodex: 3 cards, fetched 3 hours ago. This is often just no internet, as at a venue. Check the log for details.", box.TextContent);
        Assert.Equal("No such host is known.", box.QuerySelector(".text")!.GetAttribute("title")); // The reason, for whoever looks
        Assert.Equal("/logs", box.QuerySelector("a")!.GetAttribute("href"));
        Assert.Equal("Open the log", box.QuerySelector("a")!.TextContent);
        Assert.Equal("Riftcodex", Selected(section));
        Assert.Equal(CardSourceKind.Riftcodex, _catalog.ActiveSource);
    }

    [Fact]
    public async Task TryAgain_AsksTheSameSourceAgain()
    {
        var section = RenderSection();
        _hunt.Failure = new HttpRequestException("No internet.");
        await section.InvokeAsync(() => Segment(section, "RiftHunt").Click());
        _hunt.Failure = null;

        await section.InvokeAsync(() => section.Find(".box.problem button").Click());

        Assert.Equal(2, _hunt.FetchCount);
        Assert.Equal(CardSourceKind.RiftHunt, _catalog.ActiveSource);
        section.WaitForAssertion(() => Assert.Empty(section.FindAll(".box.problem")));
    }

    // Not a failure: it wasn't tried. It says when it can be, and that the catalog stays as it is.
    [Fact]
    public async Task ASourceThatMayNotBeAskedYet_IsSaidSo_NotCalledAFailure()
    {
        var section = RenderSection();
        _hunt.NotBefore = _time.GetUtcNow().AddHours(8);

        await section.InvokeAsync(() => Segment(section, "RiftHunt").Click());

        var box = section.Find(".box");
        Assert.Equal("RiftHunt can't be asked yet", box.QuerySelector("b")!.TextContent);
        Assert.Contains("It asks to be fetched at most once a day, and Riftcaster keeps to that.", box.TextContent);
        Assert.Contains("Available again", box.TextContent);
        Assert.Contains("The catalog stays Riftcodex.", box.TextContent);
        Assert.Empty(section.FindAll(".box.problem"));
        Assert.Equal(0, _hunt.FetchCount);
        Assert.Equal("Riftcodex", Selected(section));
    }

    [Fact]
    public async Task ChoosingAgain_ClearsWhatWasSaid()
    {
        var section = RenderSection();
        _hunt.NotBefore = _time.GetUtcNow().AddHours(8);
        await section.InvokeAsync(() => Segment(section, "RiftHunt").Click());
        Assert.NotEmpty(section.FindAll(".box"));
        _hunt.NotBefore = null;

        await section.InvokeAsync(() => Segment(section, "RiftHunt").Click());

        section.WaitForAssertion(() => Assert.Empty(section.FindAll(".box")));
    }

    [Fact]
    public async Task FollowsChangesMadeElsewhere()
    {
        _huntStore.Save(new CardCatalogSnapshot([HuntJinx], _time.GetUtcNow()));
        var section = RenderSection();

        // Another device switches the source: this page follows without asking.
        await _catalog.SwitchSourceAsync(CardSourceKind.RiftHunt);

        section.WaitForAssertion(() => Assert.Equal("RiftHunt", Selected(section)));
    }

    [Fact]
    public async Task WhenRemoved_StopsListening()
    {
        _huntStore.Save(new CardCatalogSnapshot([HuntJinx], _time.GetUtcNow()));
        RenderSection();

        await DisposeComponentsAsync();

        // Would throw if the removed section still tried to render.
        await _catalog.SwitchSourceAsync(CardSourceKind.RiftHunt);
    }

    // No rule under the source's row: the note under it belongs to it.
    [Fact]
    public void TheSourceRowHasNoRuleUnderIt()
    {
        var section = RenderSection();

        var sourceRow = section.FindAll(".row").First();
        Assert.Equal("Catalog source", sourceRow.QuerySelector(".title")!.TextContent.Trim());
        Assert.True(sourceRow.ParentElement!.LastElementChild == sourceRow); // The last of its own container: no rule
        Assert.Equal("hint", sourceRow.ParentElement.NextElementSibling!.ClassName);
    }

    [Fact]
    public void ARefreshOfASourceThatWouldRefuseIsDimmed_AndComesBackWhenItsWaitIsOver()
    {
        _huntStore.Save(new CardCatalogSnapshot([HuntJinx], _time.GetUtcNow()));
        _hunt.NotBefore = _time.GetUtcNow().AddHours(21);
        var section = RenderSection(CardSourceKind.RiftHunt);
        Assert.True(section.Find("button.refresh").HasAttribute("disabled"));

        // The wait is over (the source says so), and the section hears of it by itself, with no click.
        _hunt.NotBefore = null;
        _time.Advance(TimeSpan.FromHours(21) + TimeSpan.FromSeconds(2));

        section.WaitForAssertion(() => Assert.False(section.Find("button.refresh").HasAttribute("disabled")));
        Assert.DoesNotContain(FactsOf(section), fact => fact.StartsWith("Next fetch allowed"));
    }

    [Fact]
    public void TheSavedCatalog_SaysWhenItWasFetchedInLocalTime()
    {
        var section = RenderSection();

        var line = FactsOf(section)[1];

        // How long ago, then the date and time, with the zone said: the time is the server computer's, not the reader's.
        Assert.Matches(@"^Last fetched: 3 hours ago \([A-Z][a-z]{2} \d{1,2}, \d{4} at \d{1,2}:\d{2} (AM|PM) UTC[+-]\d{2}:\d{2}.*\)$", line);
    }

    [Fact]
    public void WithoutALimit_ThereIsNoNextFetchLine()
    {
        var section = RenderSection();

        Assert.DoesNotContain(FactsOf(section), fact => fact.StartsWith("Next fetch allowed"));
    }

    [Fact]
    public async Task TryAgain_IsDimmedWhileTheSourceMayNotBeAskedYet()
    {
        var section = RenderSection();
        _hunt.Failure = new HttpRequestException("No internet.");
        await section.InvokeAsync(() => Segment(section, "RiftHunt").Click());
        Assert.False(section.Find(".box.problem button").HasAttribute("disabled"));

        _hunt.NotBefore = _time.GetUtcNow().AddHours(1); // What a failed fetch of RiftHunt leaves
        _catalog.CancelSwitch();
        section.Render();

        var tryAgain = section.Find(".box.problem button");
        Assert.True(tryAgain.HasAttribute("disabled"));
        Assert.StartsWith("Available again", tryAgain.GetAttribute("title"));
    }

    // In Development RiftHunt isn't asked: its sample stands in, and the section says so and how to ask it.
    [Fact]
    public void ASampleInPlaceOfRiftHunt_IsSaidSo()
    {
        _hunt.IsSample = true;
        _huntStore.Save(new CardCatalogSnapshot([HuntJinx], _time.GetUtcNow()));

        var section = RenderSection(CardSourceKind.RiftHunt);

        var notice = section.Find(".hint.sample");
        Assert.Equal(
            "Riftcaster is running in Development mode using a small built-in sample to simulate RiftHunt's real catalog. No real requests are made to RiftHunt. To work with real data from RiftHunt, start the process with --CardSources:RiftHunt:Live true.",
            notice.TextContent.Trim());
        Assert.Equal("--CardSources:RiftHunt:Live true", notice.QuerySelector("code")!.TextContent);
    }

    // There's nothing to download, so a click would do nothing: it's dimmed, and says why.
    [Fact]
    public void ASampleHasNothingToRefresh_SoRefreshNowIsDimmed()
    {
        _hunt.IsSample = true;
        _huntStore.Save(new CardCatalogSnapshot([HuntJinx], _time.GetUtcNow()));

        var section = RenderSection(CardSourceKind.RiftHunt);

        var refresh = section.Find("button.refresh");
        Assert.True(refresh.HasAttribute("disabled"));
        Assert.Contains("built-in sample for Development", refresh.GetAttribute("title"));
        Assert.Equal(ContinuesWorking, section.Find(".control .note").TextContent.Trim());
    }

    [Fact]
    public void TheRealSources_CanBeRefreshed_AndSayNothingOfWhyNot()
    {
        var section = RenderSection();

        var refresh = section.Find("button.refresh");
        Assert.False(refresh.HasAttribute("disabled"));
        Assert.Null(refresh.GetAttribute("title"));
    }

    [Fact]
    public void TheSavedCatalogsFactsAreAnUnorderedList()
    {
        var section = RenderSection();

        var list = section.Find("ul.facts");
        Assert.Equal("ul", list.TagName.ToLowerInvariant());
        Assert.Equal(2, list.QuerySelectorAll("li").Length);
    }

    // Two boxes, not one with two paragraphs: what to know about the source, and what's true of this copy.
    [Fact]
    public void TheSourceAndTheSample_AreBoxesOfTheirOwn()
    {
        _hunt.IsSample = true;
        _huntStore.Save(new CardCatalogSnapshot([HuntJinx], _time.GetUtcNow()));

        var section = RenderSection(CardSourceKind.RiftHunt);

        var boxes = section.FindAll(".hint");
        Assert.Equal(2, boxes.Count);
        Assert.Contains("serves their whole catalog as one download", boxes[0].TextContent);
        Assert.DoesNotContain("Development", boxes[0].TextContent);
        Assert.Contains("Development mode", boxes[1].TextContent);
        Assert.DoesNotContain("serves their whole catalog", boxes[1].TextContent);
        Assert.All(boxes, box => Assert.Single(box.QuerySelectorAll("p")));
        Assert.All(boxes, box => Assert.NotNull(box.QuerySelector("svg")));
    }

    [Fact]
    public async Task TheRealSources_SayNothingOfASample()
    {
        var section = RenderSection();

        Assert.Empty(section.FindAll(".hint.sample"));
    }

    private sealed class FakeSource : ICardSource
    {
        private int _fetchCount;

        public IReadOnlyList<Card> Cards { get; set; } = [];

        public Exception? Failure { get; set; }

        public TaskCompletionSource? Gate { get; set; }

        public CardFetchProgress? ProgressBeforeGate { get; set; }

        public DateTimeOffset? NotBefore { get; set; }

        public bool IsSample { get; set; }

        public int FetchCount => _fetchCount;

        public async Task<IReadOnlyList<Card>> FetchAllAsync(IProgress<CardFetchProgress>? progress, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _fetchCount);
            if (Gate is not null)
            {
                if (ProgressBeforeGate is not null)
                {
                    progress?.Report(ProgressBeforeGate);
                }

                await Gate.Task.WaitAsync(cancellationToken);
            }

            return Failure is null ? Cards : throw Failure;
        }
    }
}
