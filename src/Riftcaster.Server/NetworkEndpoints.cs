using Riftcaster.Core.Network;

namespace Riftcaster.Server;

/// <summary>
/// Supplies the addresses the server listens on (Kestrel's endpoints) from <see cref="NetworkAccess"/>:
/// localhost while it's off, every network interface while it's on. When it changes, Kestrel
/// reloads its endpoints and rebinds, without restarting the server.
/// </summary>
/// <remarks>
/// Rebinding closes the old listener, so every connection drops for a moment; the admin and the
/// overlays reconnect by themselves. While off, nothing listens on the network at all, so Windows
/// Firewall only asks about the server the first time network access is turned on.
/// </remarks>
/// <param name="network">The network access switch, and the addresses to listen on.</param>
internal sealed class NetworkEndpointsSource(NetworkAccess network) : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder) => new NetworkEndpointsProvider(network);
}

/// <summary>
/// The configuration <see cref="NetworkEndpointsSource"/> supplies: one Kestrel endpoint per listen
/// address, reloaded whenever network access changes.
/// </summary>
internal sealed class NetworkEndpointsProvider : ConfigurationProvider
{
    private readonly NetworkAccess _network;

    public NetworkEndpointsProvider(NetworkAccess network)
    {
        _network = network;

        // Lives as long as the app, like the switch, so the handler never needs removing.
        _network.Changed += () =>
        {
            Load();
            OnReload(); // Kestrel watches its configuration section, and rebinds when it changes
        };
    }

    public override void Load()
    {
        // "*" listens on every interface, IPv4 and IPv6; "localhost" on the loopback addresses only.
        var host = _network.Enabled ? "*" : "localhost";

        Data = _network.ListenUrls
            .Select((url, i) => (Key: $"Kestrel:Endpoints:Riftcaster{i}:Url", Value: $"{url.Scheme}://{host}:{url.Port}"))
            .ToDictionary(entry => entry.Key, entry => (string?)entry.Value, StringComparer.OrdinalIgnoreCase);
    }
}
