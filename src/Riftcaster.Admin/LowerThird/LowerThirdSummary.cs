using Riftcaster.Contracts;

namespace Riftcaster.Admin.LowerThird;

/// <summary>
/// A record encapsulating how a lower third message is shown in the admin dashboard.
/// </summary>
/// <param name="Badge">The type label on the badge, for example "Keyword"</param>
/// <param name="BadgeColor">The badge's CSS color.</param>
/// <param name="Primary">The main line, for example the keyword.</param>
/// <param name="Secondary">The muted second line, for example the description.</param>
public sealed record LowerThirdSummary(string Badge, string BadgeColor, string Primary, string Secondary)
{
    /// <summary>
    /// Summarizes a message for displaying. Each message type gets a case here.
    /// </summary>
    /// <param name="message">The lower third message to summarize.</param>
    /// <returns>The lower third summary of a message</returns>
    public static LowerThirdSummary For(LowerThirdMessage message) => message switch
    {
        LowerThirdKeywordMessage keyword => new("Keyword", BadgeHue(165), keyword.Keyword, keyword.Description),
        LowerThirdInformationMessage information => new("Information", BadgeHue(250), information.Message, "Information"),
        _ => new(message.GetType().Name, "var(--text-muted)", message.GetType().Name, string.Empty),
    };

    /// <summary>
    /// A helper method to generate a CSS string for badge color.
    /// </summary>
    /// <remarks>
    /// The design gives every type badge the same lightness and chroma; only the hue varies by type.
    /// </remarks>
    /// <param name="hue">The hue of the badge color.</param>
    /// <returns>A CSS color value string.</returns>
    private static string BadgeHue(int hue) => $"oklch(0.74 0.13 {hue})";
}
