namespace Riftcaster.Core.Network;

/// <summary>
/// An access code, as kept between restarts.
/// </summary>
/// <param name="Code">The code, as it was set.</param>
/// <param name="Version">Identifies this code; see <see cref="AccessCode.Version"/>.</param>
public sealed record StoredAccessCode(string Code, string Version);
