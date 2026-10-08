using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;

namespace Riftcaster.Server.Tests;

public class DataFolderTests
{
    private static readonly string ContentRoot = Path.Combine(Path.GetTempPath(), "Riftcaster", "app");

    private static readonly string AppData = Path.Combine(Path.GetTempPath(), "Users", "someone", "AppData", "Roaming");

    [Fact]
    public void PublishedBuild_KeepsDataInTheUsersAppDataFolder()
    {
        var folder = DataFolder.Resolve(null, Environment(Environments.Production), AppData);

        Assert.Equal(Path.Combine(AppData, "Riftcaster"), folder);
        Assert.False(folder.StartsWith(ContentRoot, StringComparison.OrdinalIgnoreCase)); // Never in the app's own folder
    }

    [Fact]
    public void Development_KeepsTheProjectsDataFolder()
    {
        var folder = DataFolder.Resolve(null, Environment(Environments.Development), AppData);

        Assert.Equal(Path.Combine(ContentRoot, "data"), folder);
    }

    [Fact]
    public void NoAppDataFolder_FallsBackToTheAppsOwn()
    {
        // Some service accounts have no profile, so no app-data folder.
        var folder = DataFolder.Resolve(null, Environment(Environments.Production), appData: "");

        Assert.Equal(Path.Combine(ContentRoot, "data"), folder);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public void Setting_ChoosesTheFolder(string environment)
    {
        var absolute = Path.Combine(Path.GetTempPath(), "RiftcasterData");

        Assert.Equal(absolute, DataFolder.Resolve(absolute, Environment(environment), AppData));
        Assert.Equal(Path.Combine(ContentRoot, "saved"), DataFolder.Resolve("saved", Environment(environment), AppData)); // Relative to the content root
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void EmptySetting_IsTheDefault(string configured)
    {
        Assert.Equal(Path.Combine(AppData, "Riftcaster"), DataFolder.Resolve(configured, Environment(Environments.Production), AppData));
    }

    [Fact]
    public void PortableCopy_KeepsDataBesideTheAppsFolder()
    {
        using var portable = new PortableFolder();

        var folder = DataFolder.Resolve(null, Environment(Environments.Production, portable.App), AppData);

        Assert.Equal(Path.Combine(portable.Root, "data"), folder); // Not in current, which updates replace
    }

    [Fact]
    public void PortableCopy_SettingStillChoosesTheFolder()
    {
        using var portable = new PortableFolder();
        var absolute = Path.Combine(Path.GetTempPath(), "RiftcasterData");

        Assert.Equal(absolute, DataFolder.Resolve(absolute, Environment(Environments.Production, portable.App), AppData));
    }

    [Fact]
    public void PortableCopy_InDevelopment_KeepsTheProjectsDataFolder()
    {
        using var portable = new PortableFolder();

        var folder = DataFolder.Resolve(null, Environment(Environments.Development, portable.App), AppData);

        Assert.Equal(Path.Combine(portable.App, "data"), folder);
    }

    private static HostingEnvironment Environment(string name, string? contentRoot = null) =>
        new() { EnvironmentName = name, ContentRootPath = contentRoot ?? ContentRoot };

    /// <summary>
    /// A portable copy's folder, laid out as Velopack's portable zip is: the marker file, with the
    /// app in current. The app's path ends in a separator, as AppContext.BaseDirectory does.
    /// </summary>
    private sealed class PortableFolder : IDisposable
    {
        public PortableFolder()
        {
            Root = Directory.CreateTempSubdirectory("riftcaster-portable-").FullName;
            File.WriteAllText(Path.Combine(Root, DataFolder.PortableMarker), "");
            App = Directory.CreateDirectory(Path.Combine(Root, "current")).FullName + Path.DirectorySeparatorChar;
        }

        public string Root { get; }

        public string App { get; }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
