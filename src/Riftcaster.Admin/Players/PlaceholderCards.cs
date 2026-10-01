namespace Riftcaster.Admin.Players;

/// <summary>
/// Sample legends, champions and battlefields from the design prototype, for the player cards' selects.
/// </summary>
/// <remarks>
/// Temporary: the card database (#14) replaces these.
/// </remarks>
public static class PlaceholderCards
{
    /// <summary>
    /// Legend names.
    /// </summary>
    public static IReadOnlyList<string> Legends { get; } =
    [
        "Kai'Sa, Daughter of the Void",
        "Jinx, Loose Cannon",
        "Viktor, Herald of the Arcane",
        "Volibear, Relentless Storm",
        "Lee Sin, Blind Monk",
        "Ahri, Nine-Tailed Fox",
        "Darius, Hand of Noxus",
        "Leona, Radiant Dawn",
        "Annie, Dark Child",
        "Master Yi, Wuju Bladesman",
        "Garen, Might of Demacia",
        "Miss Fortune, Bounty Hunter",
        "Sett, The Boss",
        "Teemo, Swift Scout",
    ];

    /// <summary>
    /// Champion names.
    /// </summary>
    public static IReadOnlyList<string> Champions { get; } =
    [
        "Kai'Sa, Survivor",
        "Jinx, Rebel",
        "Viktor, Innovator",
        "Volibear, Furious",
        "Lee Sin, Centered",
        "Ahri, Alluring",
        "Darius, Trifarian",
        "Leona, Determined",
        "Annie, Fiery",
        "Master Yi, Honed",
        "Garen, Rugged",
        "Miss Fortune, Captain",
        "Sett, Kingpin",
        "Teemo, Scout",
    ];

    /// <summary>
    /// Battlefield names.
    /// </summary>
    public static IReadOnlyList<string> Battlefields { get; } =
    [
        "Grove of the God-Willow",
        "Obelisk of Power",
        "Targon's Peak",
        "Zaun Warrens",
        "Navori Fighting Pit",
        "Void Gate",
        "Sunken Temple",
        "Ravenbloom Conservatory",
        "Monastery of Hirana",
        "Trifarian War Camp",
    ];
}
