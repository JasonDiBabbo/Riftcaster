using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Riftcaster.Contracts;
using Riftcaster.Core.Cards;
using Riftcaster.Core.Settings;

namespace Riftcaster.Server.Tests;

// The card catalog over HTTP: its state, switching to another card source (at once if it's saved, else in the
// background), and fetching again. A server of its own for each test, since they change which source is in use.
public class CardCatalogApiTests
{
    private static readonly Card Jinx = new("1", "ogn-251-298", "Jinx, Loose Cannon", null, CardType.Legend, null, "Fury / Chaos", null, "Origins", "https://cards.test/1.png", Landscape: false);

    private static readonly Card Poppy = new("2", "ogn-001-298", "Poppy, Paragon", null, CardType.Unit, CardSupertype.Champion, "Body", 4, "Origins", "https://cards.test/2.png", Landscape: false);

    private static async Task<RiftcasterWebApplicationFactory> StartAsync(Action<RiftcasterWebApplicationFactory>? arrange = null)
    {
        var factory = new RiftcasterWebApplicationFactory();
        arrange?.Invoke(factory);
        _ = factory.CreateClient(); // Starts it
        await Task.CompletedTask;
        return factory;
    }

    private static RestApiClient Api(RiftcasterWebApplicationFactory factory) => new(factory.CreateClient());

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    [Fact]
    public async Task Get_SaysWhichSourceTheCatalogIsFromAndHowBigItIs()
    {
        await using var factory = await StartAsync();
        factory.Services.GetRequiredService<ICardStore>().Save(new CardCatalogSnapshot([Jinx], DateTimeOffset.UtcNow));

        var info = await Api(factory).GetAsync<CardCatalogInfo>("/api/cards/catalog");

        Assert.Equal(CardSourceKind.Riftcodex, info.Source);
        Assert.False(info.Fetching);
        Assert.Null(info.SwitchingTo);
        Assert.Null(info.NotBefore);
    }

    [Fact]
    public async Task PutSource_OfASavedCatalog_SwitchesAtOnce_AndTheChoiceIsSaved()
    {
        await using var factory = await StartAsync(f => f.RiftHuntStore.Save(new CardCatalogSnapshot([Jinx, Poppy], DateTimeOffset.UtcNow)));

        var info = await Api(factory).SendAsync<CardCatalogInfo>(HttpMethod.Put, "/api/cards/catalog/source", """{ "source": "RiftHunt" }""");

        Assert.Equal(CardSourceKind.RiftHunt, info.Source);
        Assert.Equal(2, info.CardCount);
        Assert.Equal(0, factory.RiftHuntSource.FetchCount);
        Assert.Equal(CardSourceKind.RiftHunt, factory.Services.GetRequiredService<AppSettingsService>().Settings.Cards.Source);
        Assert.Equal(CardSourceKind.RiftHunt, factory.Services.GetRequiredService<CardCatalog>().ActiveSource);
    }

    [Fact]
    public async Task PutSource_OfASourceNotFetchedYet_StartsItInTheBackground_AndSwitchesWhenItsThere()
    {
        await using var factory = await StartAsync(f =>
        {
            f.RiftHuntSource.Cards = [Jinx, Poppy];
            f.RiftHuntSource.Gate = new TaskCompletionSource();
        });
        var api = Api(factory);

        var response = await api.SendRawAsync(HttpMethod.Put, "/api/cards/catalog/source", """{ "source": "RiftHunt" }""");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var started = await response.Content.ReadFromJsonAsync<CardCatalogInfo>();
        Assert.Equal(CardSourceKind.Riftcodex, started?.Source); // Not there yet
        await WaitUntilAsync(() => factory.Services.GetRequiredService<CardCatalog>().Status.SwitchingTo is not null);
        var during = await api.GetAsync<CardCatalogInfo>("/api/cards/catalog");
        Assert.Equal(CardSourceKind.Riftcodex, during.Source);
        Assert.Equal(CardSourceKind.RiftHunt, during.SwitchingTo);
        Assert.True(during.Fetching);

        factory.RiftHuntSource.Gate!.SetResult();

        await WaitUntilAsync(() => factory.Services.GetRequiredService<CardCatalog>().ActiveSource == CardSourceKind.RiftHunt);
        var after = await api.GetAsync<CardCatalogInfo>("/api/cards/catalog");
        Assert.Equal(CardSourceKind.RiftHunt, after.Source);
        Assert.Equal(2, after.CardCount);
        Assert.Null(after.SwitchingTo);
        Assert.Equal(CardSourceKind.RiftHunt, factory.Services.GetRequiredService<AppSettingsService>().Settings.Cards.Source);
    }

    [Fact]
    public async Task AFailedSwitch_IsInTheStateWithItsSourceAndReason_AndTheCatalogIsAsItWas()
    {
        await using var factory = await StartAsync(f => f.RiftHuntSource.Failure = new HttpRequestException("No such host is known."));
        var api = Api(factory);

        var response = await api.SendRawAsync(HttpMethod.Put, "/api/cards/catalog/source", """{ "source": "RiftHunt" }""");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        await WaitUntilAsync(() => factory.Services.GetRequiredService<CardCatalog>().Status.LastFailure is not null);

        var info = await api.GetAsync<CardCatalogInfo>("/api/cards/catalog");
        Assert.Equal(CardSourceKind.Riftcodex, info.Source);
        Assert.Equal(CardSourceKind.RiftHunt, info.LastFailure!.Source);
        Assert.Equal("No such host is known.", info.LastFailure.Reason);
        Assert.Equal(CardSourceKind.Riftcodex, factory.Services.GetRequiredService<AppSettingsService>().Settings.Cards.Source);
    }

    [Fact]
    public async Task PutSource_OfTheSourceInUse_ChangesNothing()
    {
        await using var factory = await StartAsync();

        var info = await Api(factory).SendAsync<CardCatalogInfo>(HttpMethod.Put, "/api/cards/catalog/source", """{ "source": "Riftcodex" }""");

        Assert.Equal(CardSourceKind.Riftcodex, info.Source);
        Assert.Equal(0, factory.RiftHuntSource.FetchCount);
    }

    [Fact]
    public async Task PutSource_OfASourceThatMayNotBeAskedYet_IsAConflict_AndNothingIsFetched()
    {
        await using var factory = await StartAsync(f => f.RiftHuntSource.NotBefore = DateTimeOffset.UtcNow.AddHours(5));

        var response = await Api(factory).SendRawAsync(HttpMethod.Put, "/api/cards/catalog/source", """{ "source": "RiftHunt" }""");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("may not be asked", await response.Content.ReadAsStringAsync());
        Assert.Equal(0, factory.RiftHuntSource.FetchCount);
    }

    // Its saved catalog is there, so there's nothing to ask for.
    [Fact]
    public async Task PutSource_OfASourceWithASavedCatalog_IsNotHeldUpByItsWait()
    {
        await using var factory = await StartAsync(f =>
        {
            f.RiftHuntStore.Save(new CardCatalogSnapshot([Jinx], DateTimeOffset.UtcNow));
            f.RiftHuntSource.NotBefore = DateTimeOffset.UtcNow.AddHours(5);
        });

        var info = await Api(factory).SendAsync<CardCatalogInfo>(HttpMethod.Put, "/api/cards/catalog/source", """{ "source": "RiftHunt" }""");

        Assert.Equal(CardSourceKind.RiftHunt, info.Source);
        Assert.NotNull(info.NotBefore);
    }

    [Theory]
    [InlineData("""{ "source": "Nope" }""")]
    [InlineData("""{ "source": 9 }""")]
    [InlineData("""{ }""")]
    public async Task PutSource_OfNoKnownSource_Is400(string body)
    {
        await using var factory = await StartAsync();

        var response = await Api(factory).SendRawAsync(HttpMethod.Put, "/api/cards/catalog/source", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteSource_CancelsASwitchThatIsFetching()
    {
        await using var factory = await StartAsync(f =>
        {
            f.RiftHuntSource.Cards = [Jinx];
            f.RiftHuntSource.Gate = new TaskCompletionSource();
        });
        var api = Api(factory);
        await api.SendRawAsync(HttpMethod.Put, "/api/cards/catalog/source", """{ "source": "RiftHunt" }""");
        await WaitUntilAsync(() => factory.Services.GetRequiredService<CardCatalog>().Status.SwitchingTo is not null);

        var info = await api.SendAsync<CardCatalogInfo>(HttpMethod.Delete, "/api/cards/catalog/source");

        Assert.Equal(CardSourceKind.Riftcodex, info.Source);
        await WaitUntilAsync(() => factory.Services.GetRequiredService<CardCatalog>().Status.SwitchingTo is null);
        Assert.Equal(CardSourceKind.Riftcodex, factory.Services.GetRequiredService<CardCatalog>().ActiveSource);
    }

    [Fact]
    public async Task DeleteSource_WhenNothingIsSwitching_DoesNothing()
    {
        await using var factory = await StartAsync();

        var info = await Api(factory).SendAsync<CardCatalogInfo>(HttpMethod.Delete, "/api/cards/catalog/source");

        Assert.Equal(CardSourceKind.Riftcodex, info.Source);
    }

    [Fact]
    public async Task Refresh_FetchesTheSourceInUseAgain_InTheBackground()
    {
        await using var factory = await StartAsync();
        var source = (InMemoryCardSource)factory.Services.GetRequiredService<ICardSource>();
        source.Cards = [Jinx, Poppy];
        var before = source.FetchCount;

        var response = await Api(factory).SendRawAsync(HttpMethod.Post, "/api/cards/catalog/refresh");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        await WaitUntilAsync(() => factory.Services.GetRequiredService<CardCatalog>().Cards.Count == 2);
        Assert.True(source.FetchCount > before);
    }

    [Fact]
    public async Task Refresh_OfASourceThatMayNotBeAskedYet_IsAConflict()
    {
        await using var factory = await StartAsync(f => f.RiftHuntStore.Save(new CardCatalogSnapshot([Jinx], DateTimeOffset.UtcNow)));
        var api = Api(factory);
        await api.SendAsync<CardCatalogInfo>(HttpMethod.Put, "/api/cards/catalog/source", """{ "source": "RiftHunt" }""");
        factory.RiftHuntSource.NotBefore = DateTimeOffset.UtcNow.AddHours(9);

        var response = await api.SendRawAsync(HttpMethod.Post, "/api/cards/catalog/refresh");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("RiftHunt may not be asked", await response.Content.ReadAsStringAsync());
        Assert.Equal(0, factory.RiftHuntSource.FetchCount);
    }

    // Nothing to download: the same reason the web interface dims Refresh now.
    [Fact]
    public async Task Refresh_OfASampleStandingInForRiftHunt_IsAConflict_AndTheStateSaysItIsASample()
    {
        await using var factory = await StartAsync(f =>
        {
            f.RiftHuntSource.IsSample = true;
            f.RiftHuntStore.Save(new CardCatalogSnapshot([Jinx], DateTimeOffset.UtcNow));
        });
        var api = Api(factory);
        var info = await api.SendAsync<CardCatalogInfo>(HttpMethod.Put, "/api/cards/catalog/source", """{ "source": "RiftHunt" }""");
        Assert.True(info.IsSample);

        var response = await api.SendRawAsync(HttpMethod.Post, "/api/cards/catalog/refresh");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("built-in sample", await response.Content.ReadAsStringAsync());
        Assert.Contains("CardSources:RiftHunt:Live", await response.Content.ReadAsStringAsync());
        Assert.Equal(0, factory.RiftHuntSource.FetchCount);
    }

    [Fact]
    public async Task TheRealSources_AreNotSamples()
    {
        await using var factory = await StartAsync();

        var info = await Api(factory).GetAsync<CardCatalogInfo>("/api/cards/catalog");

        Assert.False(info.IsSample);
    }

    [Fact]
    public async Task Refresh_WhileAFetchIsRunning_IsAConflict()
    {
        await using var factory = await StartAsync(f =>
        {
            f.RiftHuntSource.Cards = [Jinx];
            f.RiftHuntSource.Gate = new TaskCompletionSource();
        });
        var api = Api(factory);
        await api.SendRawAsync(HttpMethod.Put, "/api/cards/catalog/source", """{ "source": "RiftHunt" }""");
        await WaitUntilAsync(() => factory.Services.GetRequiredService<CardCatalog>().Status.Fetching);

        var response = await api.SendRawAsync(HttpMethod.Post, "/api/cards/catalog/refresh");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        factory.RiftHuntSource.Gate!.SetResult();
    }

    // The choices for the players and the Featured card overlay are copies with the old source's ids: they follow the switch, by name.
    [Fact]
    public async Task SwitchingSource_SwapsTheChosenCardsForTheNewCatalogsOwn_ByName()
    {
        var huntJinx = Jinx with { Id = "hunt-1" };
        await using var factory = await StartAsync(f => f.RiftHuntStore.Save(new CardCatalogSnapshot([huntJinx], DateTimeOffset.UtcNow)));
        var players = factory.Services.GetRequiredService<Riftcaster.Core.Players.PlayersService>();
        players.UpdatePlayer(0, player => player with { Legend = Jinx, Champion = Poppy });

        await Api(factory).SendAsync<CardCatalogInfo>(HttpMethod.Put, "/api/cards/catalog/source", """{ "source": "RiftHunt" }""");

        Assert.Equal(huntJinx, players.State.Players[0].Legend);
        Assert.Equal(Poppy, players.State.Players[0].Champion); // RiftHunt has no Poppy here: it stays
    }

    [Fact]
    public async Task SwitchingSource_SwapsTheFeaturedCardToo()
    {
        var huntJinx = Jinx with { Id = "hunt-1" };
        await using var factory = await StartAsync(f => f.RiftHuntStore.Save(new CardCatalogSnapshot([huntJinx], DateTimeOffset.UtcNow)));
        var catalog = factory.Services.GetRequiredService<CardCatalog>();
        ((InMemoryCardSource)factory.Services.GetRequiredService<ICardSource>()).Cards = [Jinx];
        await catalog.RefreshAsync(CancellationToken.None);
        var featured = factory.Services.GetRequiredService<Riftcaster.Core.FeaturedCard.FeaturedCardService>();
        Assert.True(featured.Feature(Jinx.Id));

        await Api(factory).SendAsync<CardCatalogInfo>(HttpMethod.Put, "/api/cards/catalog/source", """{ "source": "RiftHunt" }""");

        Assert.Equal(huntJinx, featured.Current);
    }

    [Fact]
    public async Task TheSearch_FindsCardsOfTheSourceInUse()
    {
        await using var factory = await StartAsync(f => f.RiftHuntStore.Save(new CardCatalogSnapshot([Poppy], DateTimeOffset.UtcNow)));
        var api = Api(factory);
        await api.SendAsync<CardCatalogInfo>(HttpMethod.Put, "/api/cards/catalog/source", """{ "source": "RiftHunt" }""");

        var found = await api.GetAsync<List<Card>>("/api/cards?search=poppy");

        Assert.Equal(["2"], found.Select(card => card.Id));
    }

    [Theory]
    [InlineData(false, false, true)] // A copy that isn't being developed asks RiftHunt itself
    [InlineData(false, true, true)]
    [InlineData(true, false, false)] // Development reads the sample, so working on it sends no requests
    [InlineData(true, true, true)] // Unless it's told to
    public void RiftHuntIsAskedForItsCatalog_ExceptInDevelopmentWithoutTheSetting(bool development, bool liveSetting, bool expected)
    {
        Assert.Equal(expected, Program.RiftHuntIsLive(development, liveSetting));
    }

    [Fact]
    public void TheSettingIsNamedAsTheDocumentationSaysItIs()
    {
        Assert.Equal("CardSources:RiftHunt:Live", Program.RiftHuntLiveSetting);
    }
}
