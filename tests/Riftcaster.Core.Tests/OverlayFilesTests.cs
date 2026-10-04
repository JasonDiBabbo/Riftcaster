using Riftcaster.Core.Overlays;

namespace Riftcaster.Core.Tests;

public class OverlayFilesTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("riftcaster-overlay-files-").FullName;

    [Fact]
    public void Find_RelativePath_IsInTheContentRoot()
    {
        Directory.CreateDirectory(Path.Combine(_root, "overlays"));

        var files = OverlayFiles.Find(_root, "overlays");

        Assert.Equal(Path.Combine(_root, "overlays"), files.Folder);
        Assert.True(files.Found);
    }

    [Fact]
    public void Find_PathOutsideTheContentRoot_IsResolved()
    {
        // As in Development, where the server project's folder points at ../overlays/dist.
        var server = Directory.CreateDirectory(Path.Combine(_root, "Riftcaster.Server")).FullName;
        Directory.CreateDirectory(Path.Combine(_root, "overlays", "dist"));

        var files = OverlayFiles.Find(server, "../overlays/dist");

        Assert.Equal(Path.Combine(_root, "overlays", "dist"), files.Folder);
        Assert.True(files.Found);
    }

    [Fact]
    public void Find_MissingFolder_IsNotFound()
    {
        var files = OverlayFiles.Find(_root, "overlays");

        Assert.Equal(Path.Combine(_root, "overlays"), files.Folder);
        Assert.False(files.Found);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Find_NoSetting_HasNoFolder(string? configuredPath)
    {
        var files = OverlayFiles.Find(_root, configuredPath);

        Assert.Null(files.Folder);
        Assert.False(files.Found);
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }
}
