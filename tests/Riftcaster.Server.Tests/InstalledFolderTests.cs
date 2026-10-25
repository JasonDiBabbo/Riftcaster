namespace Riftcaster.Server.Tests;

// An installed copy has the Update.exe that installs updates next to its folder; one running from source doesn't.
public sealed class InstalledFolderTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("riftcaster-tests-");

    public void Dispose() => _root.Delete(recursive: true);

    [Fact]
    public void WithAnUpdaterBesideTheFolder_ItIsInstalled()
    {
        var current = _root.CreateSubdirectory("current");
        File.WriteAllText(Path.Combine(_root.FullName, "Update.exe"), "");

        Assert.True(InstalledFolder.HasUpdater(current.FullName));
        Assert.True(InstalledFolder.HasUpdater(current.FullName + Path.DirectorySeparatorChar)); // As AppContext.BaseDirectory ends
    }

    [Fact]
    public void WithoutOne_ItIsNot()
    {
        var bin = _root.CreateSubdirectory("bin");

        Assert.False(InstalledFolder.HasUpdater(bin.FullName));
    }

    [Fact]
    public void AFolderWithNoParent_IsNot()
    {
        Assert.False(InstalledFolder.HasUpdater(Path.GetPathRoot(_root.FullName)!));
    }
}
