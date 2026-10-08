namespace Riftcaster.Core.Network;

/// <summary>
/// What <see cref="IAccessCodeStore.Load"/> found.
/// </summary>
/// <param name="Code">The saved code, or <see langword="null"/> if there's none or it couldn't be read.</param>
/// <param name="Unreadable">
/// Whether a code was saved but couldn't be read, for example because it was saved on another
/// computer or Windows account, whose encryption keys this one doesn't have.
/// </param>
public readonly record struct LoadedAccessCode(StoredAccessCode? Code, bool Unreadable = false);
