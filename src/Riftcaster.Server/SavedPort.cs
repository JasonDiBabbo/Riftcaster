using System.Text.Json;
using Riftcaster.Core.Network;

namespace Riftcaster.Server;

/// <summary>
/// Reads the port chosen on the Settings page straight from the saved settings, since the server
/// needs it before it has any services to read it with.
/// </summary>
/// <remarks>
/// Only looks: it never moves a bad file aside or logs it. The settings service reads the file again
/// once the server has started, and reports a file it can't use there, where there is a log to say it in.
/// </remarks>
internal static class SavedPort
{
    /// <summary>
    /// The chosen port.
    /// </summary>
    /// <param name="settingsFile">The saved settings file.</param>
    /// <returns>The port, or null if there is no file, no port in it, or none that can be chosen.</returns>
    public static int? Read(string settingsFile)
    {
        try
        {
            if (!File.Exists(settingsFile))
            {
                return null;
            }

            using var stream = File.OpenRead(settingsFile);
            using var document = JsonDocument.Parse(stream);

            // The names as the settings service reads them, whatever the case.
            return Property(document.RootElement, "server") is { ValueKind: JsonValueKind.Object } server
                && Property(server, "port") is { ValueKind: JsonValueKind.Number } port
                && port.TryGetInt32(out var value)
                && ListenPort.IsValid(value)
                    ? value
                    : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static JsonElement? Property(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return null;
    }
}
