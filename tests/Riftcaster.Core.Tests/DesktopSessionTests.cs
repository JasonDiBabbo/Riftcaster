using Riftcaster.Core.Diagnostics;

namespace Riftcaster.Core.Tests;

public sealed class DesktopSessionTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("riftcaster-tests-");

    public void Dispose()
    {
        _directory.Delete(recursive: true);
    }

    private static Func<string, string?> Variables(params string[] names) => name => names.Contains(name) ? ":0" : null;

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)] // A service: nothing to show a window on
    public void Windows_NeedsASessionOfAUser(bool userInteractive, bool expected)
    {
        Assert.Equal(expected, DesktopSession.Exists(isWindows: true, isMacOS: false, userInteractive, Variables(), _ => false));
    }

    [Fact]
    public void MacOS_AlwaysHasOne()
    {
        Assert.True(DesktopSession.Exists(isWindows: false, isMacOS: true, userInteractive: false, Variables(), _ => false));
    }

    [Theory]
    [InlineData("DISPLAY")]
    [InlineData("WAYLAND_DISPLAY")]
    public void Linux_NeedsAGraphicalSessionAndXdgOpen(string variable)
    {
        Assert.True(DesktopSession.Exists(false, false, false, Variables(variable), command => command == "xdg-open"));
    }

    [Fact]
    public void Linux_WithoutAGraphicalSession_HasNone()
    {
        Assert.False(DesktopSession.Exists(false, false, false, Variables(), _ => true));
    }

    [Fact]
    public void Linux_WithoutXdgOpen_HasNone()
    {
        Assert.False(DesktopSession.Exists(false, false, false, Variables("DISPLAY"), _ => false));
    }

    [Fact]
    public void Linux_TreatsAnEmptyVariableAsUnset()
    {
        Assert.False(DesktopSession.Exists(false, false, false, name => name == "DISPLAY" ? "" : null, _ => true));
    }

    [Fact]
    public void OnPath_FindsACommandInAnyFolderOfThePath()
    {
        File.WriteAllText(Path.Combine(_directory.FullName, "xdg-open"), "");
        var path = string.Join(Path.PathSeparator, "/nowhere", _directory.FullName);

        Assert.True(DesktopSession.OnPath("xdg-open", path));
        Assert.False(DesktopSession.OnPath("other-command", path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void OnPath_WithNoPath_FindsNothing(string? path)
    {
        Assert.False(DesktopSession.OnPath("xdg-open", path));
    }

    // Whether this computer has a desktop depends on the computer: only that asking doesn't fail.
    [Fact]
    public void Current_CanBeAsked()
    {
        _ = DesktopSession.Current;
    }
}
