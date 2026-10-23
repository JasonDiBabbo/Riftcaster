using Microsoft.AspNetCore.Connections;
using Riftcaster.Core.Network;

namespace Riftcaster.Server;

/// <summary>
/// What to say, and how to tell, when Riftcaster can't start because another program is using its port.
/// </summary>
public static class PortConflict
{
    /// <summary>
    /// Whether starting failed because the port is taken, whatever the server wrapped that in.
    /// </summary>
    /// <param name="exception">What starting threw.</param>
    public static bool IsPortInUse(Exception exception)
    {
        for (var inner = exception; inner is not null; inner = inner.InnerException)
        {
            if (inner is AddressInUseException)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// What the console server says, in place of the error it would stop with, when its port is taken.
    /// </summary>
    /// <param name="port">The port that's in use.</param>
    /// <param name="source">Where the port came from, which decides what can be done about it.</param>
    /// <param name="suggestion">A port that's free, or null if none was found.</param>
    public static string ConsoleMessage(int port, PortSource source, int? suggestion)
    {
        var option = suggestion is { } free ? $"--urls http://localhost:{free}" : "--urls http://localhost:<port>";
        var cause = source switch
        {
            PortSource.LaunchOption => $"Port {port}, from the --urls option, is already in use by another program.",
            PortSource.Saved => $"Port {port}, the one chosen in Settings, is already in use by another program.",
            _ => $"Port {port} is already in use by another program. Is Riftcaster already running?",
        };

        return $"{cause}\nStart Riftcaster on a different port with {option}, or close the program that's using {port}.";
    }
}
