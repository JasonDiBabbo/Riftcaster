using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Riftcaster.Core.Network;

/// <summary>
/// Finds out whether a port is free to listen on, by trying to.
/// </summary>
/// <remarks>
/// A best guess: another program can take the port a moment after, or hold it on an address this
/// doesn't try. What switches the server to a port still checks that it works (see <see cref="PortSwitcher"/>).
/// </remarks>
public static class PortProbe
{
    /// <summary>
    /// Whether nothing else is listening on a port, on the addresses the server would use.
    /// </summary>
    /// <param name="port">The port.</param>
    /// <param name="allInterfaces">
    /// Whether the server listens on every network interface (network access is on), not only on this
    /// computer, which a program holding the port on another interface would also stop.
    /// </param>
    public static bool IsFree(int port, bool allInterfaces)
    {
        foreach (var address in Addresses(allInterfaces))
        {
            try
            {
                using var listener = new TcpListener(address, port);
                listener.Start();
                listener.Stop();
            }
            catch (SocketException)
            {
                // Taken, or one Windows keeps for itself: the server couldn't have it either.
                return false;
            }
        }

        // Binding doesn't see everything: Windows lets the server bind localhost on a port another program
        // holds on every interface, and then the two share it, unpredictably. Who's listening is asked directly.
        return !IsListenedOn(port, allInterfaces);
    }

    // Whether a program listens on the port on an address the server would use, or on every interface. Listening
    // on another interface's address alone only matters when the server listens there too.
    private static bool IsListenedOn(int port, bool allInterfaces)
    {
        return IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(endpoint =>
            endpoint.Port == port
            && (allInterfaces
                || IPAddress.IsLoopback(endpoint.Address)
                || endpoint.Address.Equals(IPAddress.Any)
                || endpoint.Address.Equals(IPAddress.IPv6Any)));
    }

    private static IEnumerable<IPAddress> Addresses(bool allInterfaces)
    {
        yield return IPAddress.Loopback;
        if (Socket.OSSupportsIPv6)
        {
            yield return IPAddress.IPv6Loopback;
        }

        if (allInterfaces)
        {
            yield return IPAddress.Any;
            if (Socket.OSSupportsIPv6)
            {
                yield return IPAddress.IPv6Any;
            }
        }
    }
}
