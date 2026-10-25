namespace Riftcaster.Core.Updates;

/// <summary>
/// How far updating has got.
/// </summary>
public enum UpdatePhase
{
    /// <summary>Nothing has been checked yet since Riftcaster started.</summary>
    NotChecked,

    /// <summary>Looking for a newer version, and downloading it if there is one.</summary>
    Checking,

    /// <summary>This is the newest version.</summary>
    UpToDate,

    /// <summary>A newer version is downloaded and waiting to be installed.</summary>
    Ready,

    /// <summary>The last check or download didn't work, often just for want of internet.</summary>
    Failed,
}

/// <summary>
/// What updating has found out.
/// </summary>
/// <param name="Phase">How far it's got.</param>
/// <param name="CheckedAt">When the last check finished, if one has.</param>
/// <param name="ReadyVersion">The downloaded version, in the <see cref="UpdatePhase.Ready"/> phase.</param>
public sealed record UpdateStatus(UpdatePhase Phase, DateTimeOffset? CheckedAt, string? ReadyVersion);
