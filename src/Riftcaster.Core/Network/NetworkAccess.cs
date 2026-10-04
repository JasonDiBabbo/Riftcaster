using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Riftcaster.Core.Network;

/// <summary>
/// Whether other devices on the network can reach the server. Off, only this computer can.
/// </summary>
/// <remarks>
/// Off every time the server starts, unless started with <c>--network on</c>: a server carried to
/// venues shouldn't open itself up just because it was open last time. While on, other devices
/// can open the overlays; the admin stays on this computer only (issue #16).
/// </remarks>
public sealed class NetworkAccess
{
    /// <summary>
    /// The address the server listens on when started without any.
    /// </summary>
    public static readonly Uri DefaultUrl = new("http://localhost:5062");

    private int _enabled;

    /// <summary>
    /// Creates the switch.
    /// </summary>
    /// <param name="enabled">Whether network access starts on.</param>
    /// <param name="listenUrls">
    /// The addresses the server listens on. Only their scheme and port count: the host is
    /// localhost while network access is off, and every network interface while it's on.
    /// </param>
    public NetworkAccess(bool enabled, IReadOnlyList<Uri> listenUrls)
    {
        _enabled = enabled ? 1 : 0;
        ListenUrls = listenUrls;
    }

    /// <summary>
    /// Whether other devices on the network can reach the server.
    /// </summary>
    public bool Enabled => Volatile.Read(ref _enabled) == 1;

    /// <summary>
    /// The addresses the server listens on (their scheme and port; see the constructor).
    /// </summary>
    public IReadOnlyList<Uri> ListenUrls { get; }

    /// <summary>
    /// Raised after <see cref="Enabled"/> changes. Handlers read <see cref="Enabled"/> for the new value.
    /// </summary>
    /// <remarks>
    /// May be raised on any thread.
    /// </remarks>
    public event Action? Changed;

    /// <summary>
    /// Turns network access on or off. Does nothing if it's already that way.
    /// </summary>
    /// <param name="enabled">Whether other devices should be able to reach the server.</param>
    public void SetEnabled(bool enabled)
    {
        var value = enabled ? 1 : 0;
        if (Interlocked.Exchange(ref _enabled, value) == value)
        {
            return;
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// The addresses other devices would use to reach the server, e.g. "http://192.168.1.20:5062",
    /// one per listen address and network address of this computer. Whether or not network
    /// access is on: they're where the server will be once it is.
    /// </summary>
    public IReadOnlyList<string> NetworkUrls() =>
        [.. ListenUrls.SelectMany(url => FindNetworkAddresses().Select(address => $"{url.Scheme}://{address}:{url.Port}"))];

    /// <summary>
    /// Whether a connection comes from this computer: a loopback address, including IPv4 loopback
    /// as seen on a dual-mode IPv6 socket. In-memory test requests have no address, and count as local.
    /// </summary>
    /// <param name="address">The connection's remote address.</param>
    public static bool IsLocal(IPAddress? address) =>
        address is null || IPAddress.IsLoopback(address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address);

    /// <summary>
    /// Reads the <c>--network</c> startup option: "on" or "off", ignoring case. Anything else,
    /// including no option, is off.
    /// </summary>
    /// <param name="value">The option's value, or <see langword="null"/> if it wasn't given.</param>
    /// <param name="enabled">Whether the option turns network access on.</param>
    /// <returns><see langword="true"/> if the value was understood (or missing), <see langword="false"/> otherwise.</returns>
    public static bool TryParseOption(string? value, out bool enabled)
    {
        enabled = string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
        return value is null || enabled || string.Equals(value, "off", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reads the addresses the server was started with (--urls, ASPNETCORE_URLS or the launch
    /// profile): semicolon-separated, falling back to <see cref="DefaultUrl"/>.
    /// </summary>
    /// <param name="urls">The "urls" setting, or <see langword="null"/> if it isn't set.</param>
    public static IReadOnlyList<Uri> ParseUrls(string? urls)
    {
        var parsed = (urls ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            // "*" and "+" (any address) aren't valid URI hosts; only the scheme and port are kept anyway.
            .Select(url => url.Replace("://*", "://localhost").Replace("://+", "://localhost"))
            .Select(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri : null)
            .OfType<Uri>()
            .ToList();

        return parsed.Count > 0 ? parsed : [DefaultUrl];
    }

    /// <summary>
    /// This computer's IPv4 addresses on its networks: those on adapters with a default gateway
    /// (the network the venue's devices are on), or every non-loopback one if none has a gateway.
    /// Skips link-local addresses (169.254.x.x), which mean an adapter has no network.
    /// </summary>
    private static IReadOnlyList<IPAddress> FindNetworkAddresses()
    {
        var adapters = NetworkInterface.GetAllNetworkInterfaces()
            .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up
                && adapter.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
            .Select(adapter => adapter.GetIPProperties())
            .Select(properties => (
                HasGateway: properties.GatewayAddresses.Any(gateway => gateway.Address.AddressFamily == AddressFamily.InterNetwork && !gateway.Address.Equals(IPAddress.Any)),
                Addresses: properties.UnicastAddresses
                    .Select(unicast => unicast.Address)
                    .Where(address => address.AddressFamily == AddressFamily.InterNetwork
                        && !IPAddress.IsLoopback(address)
                        && !address.ToString().StartsWith("169.254.", StringComparison.Ordinal))
                    .ToList()))
            .Where(adapter => adapter.Addresses.Count > 0)
            .ToList();

        var preferred = adapters.Where(adapter => adapter.HasGateway).ToList();
        return [.. (preferred.Count > 0 ? preferred : adapters).SelectMany(adapter => adapter.Addresses).Distinct()];
    }
}
