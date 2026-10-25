using Riftcaster.Contracts;
using Riftcaster.Core.Settings;

namespace Riftcaster.Core.Tests;

// The updates part of the Settings page's settings: its defaults, and what's done with a saved file that
// has something else in it.
public class UpdateSettingsTests
{
    private readonly InMemoryAppSettingsStore _store = new();

    [Fact]
    public void Defaults_AreAutomatic_AndNoChannelChosen()
    {
        var service = new AppSettingsService(_store);

        Assert.Equal(UpdateMode.Automatic, service.Settings.Updates.Mode);
        Assert.Null(service.Settings.Updates.Channel);
    }

    [Fact]
    public void AChange_IsSaved()
    {
        var service = new AppSettingsService(_store);

        service.Update(settings => settings with { Updates = new UpdateSettings { Mode = UpdateMode.Off, Channel = UpdateChannel.PreRelease } });

        Assert.Equal(UpdateMode.Off, _store.Settings!.Updates.Mode);
        Assert.Equal(UpdateChannel.PreRelease, _store.Settings.Updates.Channel);
    }

    // The JSON accepts numbers for an enum, so a hand-edited file can hold one that isn't.
    [Fact]
    public void AModeOrChannelNobodyKnows_IsPutBackToTheDefault()
    {
        _store.Save(new AppSettings { Updates = new UpdateSettings { Mode = (UpdateMode)9, Channel = (UpdateChannel)7 } });

        var service = new AppSettingsService(_store);

        Assert.Equal(UpdateMode.Automatic, service.Settings.Updates.Mode);
        Assert.Null(service.Settings.Updates.Channel);
    }

    [Fact]
    public void AChannelThatIsKnown_IsKept()
    {
        _store.Save(new AppSettings { Updates = new UpdateSettings { Mode = UpdateMode.AskFirst, Channel = UpdateChannel.Stable } });

        var service = new AppSettingsService(_store);

        Assert.Equal(UpdateMode.AskFirst, service.Settings.Updates.Mode);
        Assert.Equal(UpdateChannel.Stable, service.Settings.Updates.Channel);
    }
}
