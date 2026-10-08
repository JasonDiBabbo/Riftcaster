using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Riftcaster.Core.Network;
using Riftcaster.Core.Storage;

namespace Riftcaster.Server;

/// <summary>
/// Keeps the access code in a JSON file, encrypted with ASP.NET Core Data Protection (on Windows,
/// keys protected by the user's own Windows account), so it isn't readable as plain text on disk
/// but the admin can still show it on this computer.
/// </summary>
/// <param name="path">The file. Its folder is created on the first save.</param>
/// <param name="dataProtection">Encrypts and decrypts the code.</param>
/// <param name="logger">Logs a file that can't be read or decrypted, since those never throw.</param>
internal sealed class ProtectedAccessCodeStore(string path, IDataProtectionProvider dataProtection, ILogger<ProtectedAccessCodeStore> logger) : IAccessCodeStore
{
    private readonly JsonDocumentFile<AccessCodeFile> _file = new(path, logger);

    private readonly IDataProtector _protector = dataProtection.CreateProtector("Riftcaster.AccessCode");

    /// <inheritdoc/>
    public LoadedAccessCode Load()
    {
        if (_file.Read()?.Protected is not { } protectedCode)
        {
            return default;
        }

        try
        {
            return new(JsonSerializer.Deserialize<StoredAccessCode>(_protector.Unprotect(protectedCode), JsonSerializerOptions.Web));
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException)
        {
            // E.g. it was saved on another computer or Windows account, or the keys were lost.
            logger.LogWarning(exception, "Couldn't read the saved access code, so a new one replaces it. Show it in the admin dashboard's network panel.");
            return new(null, Unreadable: true);
        }
    }

    /// <inheritdoc/>
    public void Save(StoredAccessCode? code) =>
        _file.Write(new AccessCodeFile(code is null ? null : _protector.Protect(JsonSerializer.Serialize(code, JsonSerializerOptions.Web))));

    // The file's shape: the encrypted code, or null for none.
    private sealed record AccessCodeFile(string? Protected);
}
