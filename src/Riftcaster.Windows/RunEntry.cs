using Microsoft.Win32;
using Riftcaster.Core.Startup;

namespace Riftcaster.Windows;

/// <summary>
/// The entry that starts Riftcaster when its user signs in to Windows: a value in the current user's Run list,
/// which is also what Task Manager's Startup apps tab shows and switches. No administrator rights, and nothing for
/// other users of the computer.
/// </summary>
/// <remarks>
/// <para>
/// Points at the running exe with no arguments, so what happens at sign-in is what the settings say, such as
/// whether to open the web interface. An installed copy's exe is in <c>current</c>, which an update replaces
/// with the new version's files at the same path, so the entry keeps working.
/// </para>
/// <para>
/// Task Manager's Disable doesn't remove the Run value: it records in <c>StartupApproved</c> that Windows
/// shouldn't run it. An entry that's disabled there counts as off here, and turning it on enables it again.
/// </para>
/// </remarks>
internal sealed class RunEntry : IStartupEntry
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    private const string ValueName = "Riftcaster";

    /// <inheritdoc />
    public bool Exists
    {
        get
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKey);
            return run?.GetValue(ValueName) is string && !IsDisabled();
        }
    }

    /// <inheritdoc />
    public void Set(bool on)
    {
        if (on)
        {
            using (var run = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                run.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
            }

            ClearDisabled();
        }
        else
        {
            Remove();
        }
    }

    /// <summary>
    /// Removes the entry, as uninstalling does so that nothing is left pointing at an exe that's gone.
    /// </summary>
    public static void Remove()
    {
        using (var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true))
        {
            run?.DeleteValue(ValueName, throwOnMissingValue: false);
        }

        ClearDisabled();
    }

    // Windows writes 12 bytes there: the first is 2 (or 6) for enabled and 3 for disabled.
    private static bool IsDisabled()
    {
        using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey);
        return approved?.GetValue(ValueName) is byte[] { Length: > 0 } flags && (flags[0] & 1) == 1;
    }

    private static void ClearDisabled()
    {
        using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey, writable: true);
        approved?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
