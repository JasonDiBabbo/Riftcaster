using Riftcaster.Contracts;
using Riftcaster.Core.Cards;

namespace Riftcaster.Core.Tests;

public class RiftcodexNamesTests
{
    // Real names from Riftcodex, and what the admin and overlays should show for them.
    [Theory]
    [InlineData("Star Spring", CardType.Battlefield, "Star Spring", null)]
    [InlineData("Xerath - Freed", CardType.Unit, "Xerath, Freed", null)]
    [InlineData("Shen, Scourge of Shadows", CardType.Unit, "Shen, Scourge of Shadows", null)] // Already normalized
    [InlineData("Poppy - Paragon (Alternate Art)", CardType.Unit, "Poppy, Paragon", "Alternate Art")]
    [InlineData("Vi - Piltover Enforcer (Signature)", CardType.Legend, "Vi, Piltover Enforcer", "Signature")]
    [InlineData("Yasuo - Unforgiven (Metal)", CardType.Legend, "Yasuo, Unforgiven", "Metal")]
    [InlineData("Sprite (274) // Buff", CardType.Unit, "Sprite (274) // Buff", null)] // Brackets mid-name aren't a variant
    [InlineData("  Teemo - Swift Scout (Starter)  ", CardType.Legend, "Teemo, Swift Scout", "Starter")]
    [InlineData("Yordle, Kennen - Heart of the Tempest", CardType.Legend, "Kennen, Heart of the Tempest", null)] // Tribe dropped
    [InlineData("Yordle, Kennen - Heart of the Tempest (Overnumbered)", CardType.Legend, "Kennen, Heart of the Tempest", "Overnumbered")]
    public void Normalize_SplitsVariantAndUsesCommaForm(string name, CardType type, string expectedName, string? expectedVariant)
    {
        var (normalized, variant) = RiftcodexNames.Normalize(name, type, tags: null);

        Assert.Equal(expectedName, normalized);
        Assert.Equal(expectedVariant, variant);
    }

    [Theory]
    [InlineData("Master of Shadows", new[] { "Zed" }, "Zed, Master of Shadows")]
    [InlineData("Heart of the Tempest", new[] { "Yordle", "Kennen" }, "Kennen, Heart of the Tempest")] // Yordle isn't a champion
    [InlineData("Daughter of the Void", new string[0], "Daughter of the Void")] // No champion tag to add
    public void Normalize_LegendWithTitleOnly_AddsChampionFromTags(string name, string[] tags, string expected)
    {
        var (normalized, _) = RiftcodexNames.Normalize(name, CardType.Legend, tags);

        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void Normalize_NonLegendWithTitleOnly_IsLeftAlone()
    {
        var (normalized, _) = RiftcodexNames.Normalize("Recruit", CardType.Unit, ["Mech"]);

        Assert.Equal("Recruit", normalized);
    }
}
