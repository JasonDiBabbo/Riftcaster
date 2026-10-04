using Riftcaster.Contracts;

namespace Riftcaster.Core.LowerThird;

/// <summary>
/// What makes a lower third message complete, for messages from outside the admin (the REST API),
/// whose editor checks the same things as they're typed.
/// </summary>
public static class LowerThirdMessageRules
{
    /// <summary>
    /// Tidies a message as the admin's editor does, then checks it: text is trimmed, socials
    /// links with no handle are dropped, and every field it shows must have text.
    /// </summary>
    /// <param name="message">The message as received. Its text may be null when a field was left out.</param>
    /// <param name="errors">
    /// Each problem, keyed by its field's JSON name ("keyword", "links[1].network" and so on);
    /// empty when the message is valid.
    /// </param>
    /// <returns>The tidied message, which is only usable when <paramref name="errors"/> is empty.</returns>
    public static LowerThirdMessage Normalize(LowerThirdMessage message, out IReadOnlyDictionary<string, string[]> errors)
    {
        ArgumentNullException.ThrowIfNull(message);

        var found = new Dictionary<string, string[]>();
        errors = found;

        switch (message)
        {
            case LowerThirdKeywordMessage keyword:
                var tidiedKeyword = new LowerThirdKeywordMessage(Tidy(keyword.Keyword), Tidy(keyword.Description));
                RequireText(found, "keyword", tidiedKeyword.Keyword);
                RequireText(found, "description", tidiedKeyword.Description);
                return tidiedKeyword;

            case LowerThirdInformationMessage information:
                var tidiedInformation = new LowerThirdInformationMessage(Tidy(information.Message));
                RequireText(found, "message", tidiedInformation.Message);
                return tidiedInformation;

            case LowerThirdSocialsMessage socials:
                var links = socials.Links ?? [];
                for (var i = 0; i < links.Count; i++)
                {
                    if (links[i] is null)
                    {
                        found[$"links[{i}]"] = ["A link can't be null."];
                    }
                    else if (!Enum.IsDefined(links[i].Network))
                    {
                        found[$"links[{i}].network"] = [$"Use one of: {string.Join(", ", Enum.GetNames<SocialNetwork>())}."];
                    }
                }

                var tidiedLinks = links
                    .Where(link => link is not null && !string.IsNullOrWhiteSpace(link.Handle))
                    .Select(link => link with { Handle = Tidy(link.Handle) })
                    .ToList();
                if (tidiedLinks.Count == 0 && found.Count == 0)
                {
                    found["links"] = ["Add at least one link with a handle."];
                }

                return new LowerThirdSocialsMessage(tidiedLinks);

            default:
                throw new ArgumentException($"Unknown kind of lower third: {message.GetType().Name}.", nameof(message));
        }
    }

    private static string Tidy(string? text) => text?.Trim() ?? "";

    private static void RequireText(Dictionary<string, string[]> errors, string field, string text)
    {
        if (text.Length == 0)
        {
            errors[field] = ["Required."];
        }
    }
}
