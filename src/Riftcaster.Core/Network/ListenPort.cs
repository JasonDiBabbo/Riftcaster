using System.Globalization;

namespace Riftcaster.Core.Network;

/// <summary>
/// The port the web interface and the overlays are served on: which ones can be chosen, and how
/// someone's typing is read.
/// </summary>
public static class ListenPort
{
    /// <summary>
    /// The lowest port that can be chosen. Below it are the well-known ports, which a program needs
    /// special rights to listen on and other programs use.
    /// </summary>
    public const int Min = 1024;

    /// <summary>
    /// The highest port there is.
    /// </summary>
    public const int Max = 65535;

    /// <summary>
    /// The port used when none is chosen: the one in <see cref="NetworkAccess.DefaultUrl"/>.
    /// </summary>
    public static int Default => NetworkAccess.DefaultUrl.Port;

    /// <summary>
    /// Whether a port can be chosen: from <see cref="Min"/> to <see cref="Max"/>.
    /// </summary>
    /// <param name="port">The port.</param>
    public static bool IsValid(int port) => port is >= Min and <= Max;

    /// <summary>
    /// Reads a port someone typed: digits only, around which spaces are ignored, within the range
    /// that can be chosen.
    /// </summary>
    /// <param name="text">What was typed.</param>
    /// <param name="port">The port, if it was one that can be chosen.</param>
    /// <returns>Whether it was.</returns>
    public static bool TryParse(string? text, out int port) =>
        int.TryParse(text?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out port) && IsValid(port);

    /// <summary>
    /// The first port after another that nothing is using: what to suggest when the one wanted is taken.
    /// </summary>
    /// <param name="taken">The port that's in use.</param>
    /// <param name="allInterfaces">Whether the server listens on every network interface (see <see cref="PortProbe.IsFree"/>).</param>
    /// <returns>The port, or null if every one above is in use too.</returns>
    public static int? NextFree(int taken, bool allInterfaces)
    {
        for (var port = Math.Max(taken + 1, Min); port <= Max; port++)
        {
            if (PortProbe.IsFree(port, allInterfaces))
            {
                return port;
            }
        }

        return null;
    }

    /// <summary>
    /// The port the server serves HTTP on, from the addresses it listens on: the first http one.
    /// </summary>
    /// <param name="listenUrls">The addresses (see <see cref="NetworkAccess.ListenUrls"/>).</param>
    /// <returns>The port, or null if there is no http address (a developer's setup, with only https ones).</returns>
    public static int? HttpPort(IEnumerable<Uri> listenUrls) =>
        listenUrls.FirstOrDefault(url => url.Scheme == Uri.UriSchemeHttp)?.Port;
}
