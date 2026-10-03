using Riftcaster.Contracts;

namespace Riftcaster.Core.FeaturedCard;

/// <summary>
/// Loads and saves the featured card, so it's still on air after a server restart.
/// </summary>
public interface IFeaturedCardStore
{
    /// <summary>
    /// Loads the saved featured card.
    /// </summary>
    /// <returns>
    /// The saved state, or <see langword="null"/> if there is none (the service then starts with nothing featured).
    /// </returns>
    FeaturedCardState? Load();

    /// <summary>
    /// Saves the featured card, replacing any previous save.
    /// </summary>
    /// <remarks>
    /// Implementations should log failures rather than throw exceptions.
    /// </remarks>
    void Save(FeaturedCardState state);
}
