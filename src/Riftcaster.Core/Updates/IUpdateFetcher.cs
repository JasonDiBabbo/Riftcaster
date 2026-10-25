using Riftcaster.Contracts;

namespace Riftcaster.Core.Updates;

/// <summary>
/// Where updates come from and how they're fetched: the Windows app's updater, which the server itself
/// knows nothing of.
/// </summary>
public interface IUpdateFetcher
{
    /// <summary>
    /// The version downloaded earlier and still waiting to be installed, or null if there is none.
    /// </summary>
    string? DownloadedVersion { get; }

    /// <summary>
    /// Looks for a newer version and downloads it.
    /// </summary>
    /// <param name="channel">Which releases to look at.</param>
    /// <param name="cancellationToken">Stops the check.</param>
    /// <returns>The version that was downloaded, or null if this one is the newest.</returns>
    Task<string?> CheckAndDownloadAsync(UpdateChannel channel, CancellationToken cancellationToken);
}
