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

    private static HostingEnvironment Environment(string name) =>
        new() { EnvironmentName = name, ContentRootPath = ContentRoot };
}
