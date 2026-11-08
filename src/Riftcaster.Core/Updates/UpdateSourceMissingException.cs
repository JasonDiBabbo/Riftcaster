namespace Riftcaster.Core.Updates;

/// <summary>
/// The place updates are to come from can't be found, such as a folder that doesn't exist. Said apart from other
/// failures because it is certain, it is the setting's doing, and whoever set it can fix it.
/// </summary>
public sealed class UpdateSourceMissingException : Exception
{
    /// <summary>
    /// Says the folder at <paramref name="folder"/> doesn't exist.
    /// </summary>
    /// <param name="folder">The folder the Updates:Source setting names.</param>
    public UpdateSourceMissingException(string folder)
        : base($"The update folder {folder} doesn't exist.")
    {
        Folder = folder;
    }

    /// <summary>
    /// The folder that was looked for.
    /// </summary>
    public string Folder { get; }
}
