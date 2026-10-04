namespace Riftcaster.Core.Network;

/// <summary>
/// An in-memory store for the access code.
/// </summary>
/// <remarks>
/// Nothing survives a server restart. For tests.
/// </remarks>
public sealed class InMemoryAccessCodeStore : IAccessCodeStore
{
    /// <summary>
    /// The most recently saved code.
    /// </summary>
    public StoredAccessCode? Code { get; private set; }

    /// <inheritdoc/>
    public StoredAccessCode? Load() => Code;

    /// <inheritdoc/>
    public void Save(StoredAccessCode? code)
    {
        Code = code;
    }
}
