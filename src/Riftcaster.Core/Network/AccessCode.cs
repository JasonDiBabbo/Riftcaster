using System.Security.Cryptography;
using System.Text;

namespace Riftcaster.Core.Network;

/// <summary>
/// The shared code that lets approved operators (casters, event coordinators) use the admin from
/// their own devices while network access is on (issue #16). Without one, the admin stays on this
/// computer only. Only this computer can set, show, change or remove it.
/// </summary>
/// <remarks>
/// Each code has a <see cref="Version"/>, which a device's sign-in records. Changing or removing
/// the code changes the version, which signs out every device signed in with the old one.
/// </remarks>
public sealed class AccessCode
{
    /// <summary>
    /// The shortest code accepted, not counting spaces and dashes.
    /// </summary>
    public const int MinLength = 6;

    /// <summary>
    /// The longest code accepted, not counting spaces and dashes.
    /// </summary>
    public const int MaxLength = 64;

    // Generated codes avoid letters and digits that are easy to confuse when read out or typed on
    // a phone: no 0/O, 1/I/L, 5/S, 2/Z or 8/B.
    private const string GeneratedAlphabet = "ACDEFGHJKMNPQRTUVWXY34679";

    private readonly IAccessCodeStore _store;

    private readonly Lock _lock = new();

    private StoredAccessCode? _current;

    /// <summary>
    /// Creates the service with the saved code, if any.
    /// </summary>
    /// <param name="store">Where the code is kept between restarts.</param>
    public AccessCode(IAccessCodeStore store)
    {
        _store = store;
        _current = store.Load();
    }

    /// <summary>
    /// Whether a code is set, so remote operators can sign in.
    /// </summary>
    public bool IsSet => _current is not null;

    /// <summary>
    /// The current code, as it was set, or <see langword="null"/> if there's none. For showing to
    /// the operator on this computer only.
    /// </summary>
    public string? Code => _current?.Code;

    /// <summary>
    /// Identifies the current code, or <see langword="null"/> if there's none. Sign-ins record it,
    /// and only count while it's still current.
    /// </summary>
    public string? Version => _current?.Version;

    /// <summary>
    /// Raised after the code is set, changed or removed. May be raised on any thread.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Sets or changes the code, signing out every device signed in with the previous one.
    /// </summary>
    /// <param name="code">The new code. Spaces and dashes don't count towards its length, or when it's typed.</param>
    /// <returns><see langword="true"/> if it's set, or <see langword="false"/> if it's too short or too long.</returns>
    public bool Set(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        var length = Normalize(code).Length;
        if (length is < MinLength or > MaxLength)
        {
            return false;
        }

        lock (_lock)
        {
            _current = new StoredAccessCode(code.Trim(), NewVersion());
            _store.Save(_current);
        }

        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Removes the code, signing out every remote device. The admin is then on this computer only.
    /// Does nothing if there's no code.
    /// </summary>
    public void Remove()
    {
        lock (_lock)
        {
            if (_current is null)
            {
                return;
            }

            _current = null;
            _store.Save(null);
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Whether an attempt matches the current code. Ignores case, spaces and dashes, so a code is
    /// easy to type on a phone. Takes the same time however close the attempt is, so timing can't
    /// reveal it.
    /// </summary>
    /// <param name="attempt">What the operator typed.</param>
    /// <returns><see langword="true"/> if it matches; always <see langword="false"/> if no code is set.</returns>
    public bool Matches(string? attempt)
    {
        var current = _current;
        if (current is null || attempt is null)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Normalize(attempt)),
            Encoding.UTF8.GetBytes(Normalize(current.Code)));
    }

    /// <summary>
    /// A new random code, easy to read out and type: eight characters in two groups, e.g. "K7QM-4XPA".
    /// </summary>
    public static string Generate()
    {
        var characters = RandomNumberGenerator.GetString(GeneratedAlphabet, 8);
        return $"{characters[..4]}-{characters[4..]}";
    }

    // Upper case, without spaces or dashes.
    private static string Normalize(string code) =>
        string.Concat(code.Where(c => !char.IsWhiteSpace(c) && c != '-')).ToUpperInvariant();

    private static string NewVersion() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
}
