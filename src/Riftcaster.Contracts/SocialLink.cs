namespace Riftcaster.Contracts;

/// <summary>
/// One link in a Socials lower third.
/// </summary>
/// <param name="Network">The platform, which picks the icon.</param>
/// <param name="Handle">What's shown next to the icon, for example "@Riftcaster" or "discord.gg/riftcaster".</param>
public record SocialLink(SocialNetwork Network, string Handle);
