using Riftcaster.Contracts;

namespace Riftcaster.Admin.Tests;

/// <summary>
/// Cards for tests, with only the fields that matter set.
/// </summary>
internal static class TestCards
{
    public static Card Legend(string id, string name, string? variant = null) =>
        Card(id, name, CardType.Legend, supertype: null, variant: variant);

    public static Card Champion(string id, string name) =>
        Card(id, name, CardType.Unit, CardSupertype.Champion);

    public static Card Card(string id, string name, CardType type, CardSupertype? supertype = null, string? variant = null, string domain = "Fury", int? energy = null, string set = "Origins") =>
        new(id, $"tst-{id}", name, variant, type, supertype, domain, energy, set, $"https://cards.test/{id}.png", Landscape: type == CardType.Battlefield);
}
