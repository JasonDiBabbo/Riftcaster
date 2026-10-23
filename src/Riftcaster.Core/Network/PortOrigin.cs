namespace Riftcaster.Core.Network;

/// <summary>
/// Where the port in use came from.
/// </summary>
public enum PortSource
{
    /// <summary>Nothing chose one, so the default (<see cref="ListenPort.Default"/>).</summary>
    Default,

    /// <summary>The one chosen on the Settings page.</summary>
    Saved,

    /// <summary>The <c>--urls</c> launch option (or <c>ASPNETCORE_URLS</c>), which wins over the Settings page.</summary>
    LaunchOption,
}

/// <summary>
/// How the server's port was decided when it started. A launch option owns the port for the whole run:
/// it can't be switched from the Settings page. Where the server is listening now is
/// <see cref="NetworkAccess.ListenUrls"/>, which a switch changes.
/// </summary>
/// <remarks>
/// Riftcaster serves plain HTTP: it has no certificate of its own. It listens on HTTPS only when started
/// with an https address in <c>--urls</c>, as Visual Studio's https profile does, which is a developer's
/// setup, so only the HTTP port is ever shown or chosen.
/// </remarks>
/// <param name="Source">Where the port came from.</param>
public sealed record PortOrigin(PortSource Source);
