using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Riftcaster.Contracts;
using Riftcaster.Core.Diagnostics;
using Riftcaster.Core.Network;
using Riftcaster.Core.Settings;
using Riftcaster.Core.Updates;
using Riftcaster.WebUI.Layout;
using Riftcaster.WebUI.Settings;

namespace Riftcaster.WebUI.Tests;

// What the Settings page's rows say. The line under a setting's title says what the setting does, in plain
// words, the same way for every setting: whichever section it's in, someone reading it learns what changing
// it changes. When it takes effect, if that isn't at once, is the small note beside the control.
public class SettingsRowsTests : BunitContext
{
    public SettingsRowsTests()
    {
        var network = new NetworkAccess(false, [NetworkAccess.DefaultUrl]);
        var settings = new AppSettingsService(new InMemoryAppSettingsStore());
        Services.AddSingleton(settings);
        Services.AddSingleton(network);
        var updates = new UpdateService(settings, "0.2.0", UpdateAvailability.FromSource, TimeProvider.System, NullLogger<UpdateService>.Instance);
        updates.Attach(new NoUpdates(), () => { });
        Services.AddSingleton(updates);
        Services.AddSingleton(TimeProvider.System);
        Services.AddSingleton(new PortOrigin(PortSource.Default));
        Services.AddSingleton(new PortSwitcher(network, settings, (_, _) => Task.FromResult(true), TimeProvider.System, NullLogger<PortSwitcher>.Instance));
        Services.AddSingleton(new LogFolderOpener(new LogFolder(Path.GetTempPath()), () => true, _ => { }, NullLogger<LogFolderOpener>.Instance));
    }

    private sealed class NoUpdates : IUpdateFetcher
    {
        public string? DownloadedVersion => null;

        public Task<string?> CheckAndDownloadAsync(UpdateChannel channel, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
    }

    private static Action<ComponentParameterCollectionBuilder<T>> AsLocal<T>() where T : Microsoft.AspNetCore.Components.IComponent =>
        parameters => parameters.AddCascadingValue(new OperatorSession(IsLocal: true, CodeVersion: null));

    private (string Title, string? Description)[] RowsOf<T>() where T : Microsoft.AspNetCore.Components.IComponent
    {
        var section = Render<T>(AsLocal<T>());
        return [.. section.FindAll(".row").Select(row =>
        {
            // The phone's copy of the note sits inside the description: it's not the description.
            var description = row.QuerySelector(".description");
            description?.QuerySelector(".note-inline")?.Remove();
            return (row.QuerySelector(".title")!.TextContent.Trim(), description?.TextContent.Trim());
        })];
    }

    [Fact]
    public void EveryMatchDefaultsRowSaysWhatItDoes()
    {
        var rows = RowsOf<MatchDefaultsSection>();

        Assert.Equal(["Mode", "Format", "Points to win", "Duration", "Allow overtime"], rows.Select(row => row.Title));
        Assert.All(rows, row => Assert.False(string.IsNullOrWhiteSpace(row.Description), $"{row.Title} has no description"));
    }

    [Fact]
    public void EveryServerRowSaysWhatItDoes()
    {
        var rows = RowsOf<ServerSection>();

        Assert.Equal(["Server port"], rows.Select(row => row.Title));
        Assert.All(rows, row => Assert.False(string.IsNullOrWhiteSpace(row.Description), $"{row.Title} has no description"));
    }

    [Fact]
    public void EveryUpdatesRowSaysWhatItDoes()
    {
        var rows = RowsOf<UpdatesSection>();

        Assert.Equal(["Updates", "Update channel", "Riftcaster 0.2.0"], rows.Select(row => row.Title));
        Assert.All(rows, row => Assert.False(string.IsNullOrWhiteSpace(row.Description), $"{row.Title} has no description"));
    }

    [Fact]
    public void EveryLoggingRowSaysWhatItDoes()
    {
        var rows = RowsOf<LoggingSection>();

        Assert.Equal(["Log detail"], rows.Select(row => row.Title));
        Assert.All(rows, row => Assert.False(string.IsNullOrWhiteSpace(row.Description), $"{row.Title} has no description"));
    }

    // The descriptions are about the setting, not about when it applies: that's the note.
    [Fact]
    public void NoDescriptionIsAboutWhenItApplies()
    {
        var rows = RowsOf<MatchDefaultsSection>().Concat(RowsOf<ServerSection>()).Concat(RowsOf<LoggingSection>()).Concat(RowsOf<UpdatesSection>());

        Assert.All(rows, row => Assert.DoesNotContain("Applies", row.Description!));
    }

    [Fact]
    public void TheServerPortSaysWhatItCoversAndWhenItApplies()
    {
        var section = Render<ServerSection>(AsLocal<ServerSection>());

        var description = section.Find(".description").TextContent;

        Assert.Contains("web interface", description);
        Assert.Contains("overlays", description);
        Assert.Contains("API", description);
        Assert.Contains("Browser Sources", description);
        Assert.Contains("--urls", description);
        Assert.Equal("Applies when you switch, or at the next start", section.Find(".control .note").TextContent.Trim());
    }

    [Fact]
    public void TheUpdateChoicesSayWhenTheyApply()
    {
        var section = Render<UpdatesSection>(AsLocal<UpdatesSection>());

        var notes = section.FindAll(".control .note").Select(note => note.TextContent.Trim());

        Assert.Equal(["Applies immediately", "Applies to the next check"], notes);
    }

    [Fact]
    public void TheLogDetailSaysWhenItApplies()
    {
        var section = Render<LoggingSection>(AsLocal<LoggingSection>());

        Assert.Equal("Applies immediately", section.Find(".control .note").TextContent.Trim());
    }
}
