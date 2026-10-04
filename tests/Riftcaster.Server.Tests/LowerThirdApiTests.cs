using System.Net.Http.Json;
using Riftcaster.Contracts;

namespace Riftcaster.Server.Tests;

// Its own test server (a fixture per class): these tests change the lower third library.
public class LowerThirdApiTests(RiftcasterWebApplicationFactory factory) : IClassFixture<RiftcasterWebApplicationFactory>
{
    private readonly RestApiClient _api = new(factory.CreateClient());

    [Fact]
    public async Task Add_SavesATidiedMessage()
    {
        var response = await _api.SendRawAsync(HttpMethod.Post, "/api/lower-third/messages",
            """{ "keyword": "  Deflect ", "description": "Costs more to target.", "type": "keyword" }"""); // "type" needn't be first

        Assert.Equal(201, (int)response.StatusCode);
        var entry = await ReadAsync<LowerThirdEntry>(response);
        Assert.Equal(new LowerThirdKeywordMessage("Deflect", "Costs more to target."), entry.Message);
        Assert.Equal($"/api/lower-third/messages/{entry.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal(entry, await _api.GetAsync<LowerThirdEntry>($"/api/lower-third/messages/{entry.Id}"));
        Assert.Contains(entry, (await _api.GetAsync<Library>("/api/lower-third")).Entries);
    }

    [Fact]
    public async Task Add_Socials_DropsEmptyHandles()
    {
        var entry = await AddAsync(new LowerThirdSocialsMessage([new(SocialNetwork.Twitch, "riftcaster"), new(SocialNetwork.X, " ")]));

        Assert.Equal(new LowerThirdSocialsMessage([new(SocialNetwork.Twitch, "riftcaster")]), entry.Message);
    }

    [Theory]
    [InlineData("""{ "type": "keyword", "keyword": "Deflect" }""", "description")]
    [InlineData("""{ "type": "information", "message": "   " }""", "message")]
    [InlineData("""{ "type": "socials", "links": [{ "network": "Twitch", "handle": "" }] }""", "links")]
    [InlineData("""{ "type": "socials", "links": [{ "network": 42, "handle": "x" }] }""", "links[0].network")]
    public async Task Add_Incomplete_Is400(string body, string field)
    {
        var errors = await _api.InvalidAsync(HttpMethod.Post, "/api/lower-third/messages", body);

        Assert.Contains(field, errors.Keys);
    }

    [Theory]
    [InlineData("""{ "type": "headline", "text": "Hi" }""", "body")]
    [InlineData("""{ "keyword": "No type" }""", "type")]
    [InlineData("{ not json", "body")]
    [InlineData("null", "body")]
    public async Task Add_UnreadableMessage_Is400(string body, string field)
    {
        var errors = await _api.InvalidAsync(HttpMethod.Post, "/api/lower-third/messages", body);

        Assert.Contains("Every lower third has a \"type\"", Assert.Single(errors[field]));
    }

    [Fact]
    public async Task ShowReplaceHideDelete()
    {
        var entry = await AddAsync(new LowerThirdInformationMessage("Round 2 starts soon"));

        var shown = await _api.SendAsync<Library>(HttpMethod.Post, $"/api/lower-third/messages/{entry.Id}/show");
        Assert.Equal(entry.Id, shown.LiveEntryId);

        var replaced = await _api.SendAsync<LowerThirdEntry>(HttpMethod.Put, $"/api/lower-third/messages/{entry.Id}",
            AsMessage(new LowerThirdKeywordMessage("Ganking", "Move to an adjacent battlefield.")));
        Assert.Equal(new LowerThirdEntry(entry.Id, new LowerThirdKeywordMessage("Ganking", "Move to an adjacent battlefield.")), replaced);
        Assert.Contains("keyword", (await _api.InvalidAsync(HttpMethod.Put, $"/api/lower-third/messages/{entry.Id}", """{ "type": "keyword" }""")).Keys);

        var hidden = await _api.SendAsync<Library>(HttpMethod.Post, "/api/lower-third/hide");
        Assert.Null(hidden.LiveEntryId);

        var deleted = await _api.SendRawAsync(HttpMethod.Delete, $"/api/lower-third/messages/{entry.Id}");
        Assert.Equal(204, (int)deleted.StatusCode);
        Assert.DoesNotContain((await _api.GetAsync<Library>("/api/lower-third")).Entries, saved => saved.Id == entry.Id);
    }

    [Theory]
    [InlineData("GET", "")]
    [InlineData("PUT", "")]
    [InlineData("DELETE", "")]
    [InlineData("POST", "/show")]
    public async Task UnknownId_Is404(string method, string suffix)
    {
        var id = Guid.NewGuid();
        var body = method == "PUT" ? AsMessage(new LowerThirdInformationMessage("Hello")) : null;

        var detail = await _api.NotFoundAsync(new HttpMethod(method), $"/api/lower-third/messages/{id}{suffix}", body);

        Assert.Equal($"No saved lower third with id {id}.", detail);
    }

    private async Task<LowerThirdEntry> AddAsync(LowerThirdMessage message)
    {
        var response = await _api.SendRawAsync(HttpMethod.Post, "/api/lower-third/messages", AsMessage(message));
        Assert.Equal(201, (int)response.StatusCode);
        return await ReadAsync<LowerThirdEntry>(response);
    }

    // As a LowerThirdMessage, so the JSON has its "type".
    private static JsonContent AsMessage(LowerThirdMessage message) => JsonContent.Create(message);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>())!;

    private sealed record Library(List<LowerThirdEntry> Entries, Guid? LiveEntryId);
}
