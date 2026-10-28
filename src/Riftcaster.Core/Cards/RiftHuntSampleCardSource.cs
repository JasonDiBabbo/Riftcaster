using System.Reflection;
using Microsoft.Extensions.Logging;
using Riftcaster.Contracts;

namespace Riftcaster.Core.Cards;

/// <summary>
/// A small sample of RiftHunt's catalog, read from a file built into Riftcaster, in place of fetching it. For
/// Development, so that running the server while working on it doesn't ask RiftHunt for anything: the real
/// source is used there only if it's turned on (<c>CardSources:RiftHunt:Live</c>).
/// </summary>
/// <param name="logger">Logs what was skipped, as the real source does.</param>
public sealed class RiftHuntSampleCardSource(ILogger<RiftHuntSampleCardSource> logger) : ICardSource
{
    /// <summary>
    /// The name of the sample file inside Riftcaster.Core.
    /// </summary>
    public const string ResourceName = "rifthunt-sample.json";

    /// <summary>
    /// The sample file's text: RiftHunt's bulk card file with 37 of its cards.
    /// </summary>
    public static string SampleJson()
    {
        // Built into Riftcaster.Core (see its project file), so it's always there.
        using var stream = typeof(RiftHuntSampleCardSource).Assembly.GetManifestResourceStream(ResourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <inheritdoc/>
    public bool IsSample => true;

    /// <inheritdoc/>
    public Task<IReadOnlyList<Card>> FetchAllAsync(IProgress<CardFetchProgress>? progress, CancellationToken cancellationToken)
    {
        logger.LogInformation("Reading the sample of the RiftHunt catalog built into Riftcaster; RiftHunt isn't asked.");
        return Task.FromResult(RiftHuntCards.Read(SampleJson(), logger));
    }
}
