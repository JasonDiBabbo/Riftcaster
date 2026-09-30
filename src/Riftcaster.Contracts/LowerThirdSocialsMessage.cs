namespace Riftcaster.Contracts;

/// <summary>
/// A lower third that cycles through social media links, one at a time.
/// </summary>
/// <param name="Links">The links, in the order the overlay cycles through them.</param>
public record LowerThirdSocialsMessage(IReadOnlyList<SocialLink> Links) : LowerThirdMessage
{
    /// <summary>
    /// Compares this <see cref="LowerThirdSocialsMessage"/> for equality to another.
    /// </summary>
    /// <remarks>
    /// A record's generated equality compares each property with ==, which for a list
    /// means "the same list object". Compare the links themselves instead, in order,
    /// so two messages with the same links are equal wherever their lists came from.
    /// </remarks>
    /// <param name="other">The message to compare with.</param>
    /// <returns>
    /// <see langword="true"/> if both are Socials messages with the same links
    /// in the same order and <see langword="false"/> if not.
    /// </returns>
    public virtual bool Equals(LowerThirdSocialsMessage? other) =>
        base.Equals(other) && Links.SequenceEqual(other.Links);

    /// <summary>
    /// Hashes the links' values, to match <see cref="Equals(LowerThirdSocialsMessage?)"/>:
    /// equal objects must have equal hash codes.
    /// </summary>
    /// <returns>A hash code.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(base.GetHashCode());
        foreach (var link in Links)
        {
            hash.Add(link);
        }

        return hash.ToHashCode();
    }
}
