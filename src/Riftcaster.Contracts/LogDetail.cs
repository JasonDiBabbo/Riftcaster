using System.Text.Json.Serialization;

namespace Riftcaster.Contracts;

/// <summary>
/// How much the server writes to its log (the console and the log files).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<LogDetail>))]
public enum LogDetail
{
    /// <summary>Only problems: warnings and errors.</summary>
    WarningsOnly,

    /// <summary>Startup, updates, card refreshes and warnings. Right for everyday use.</summary>
    Normal,

    /// <summary>Everything Normal logs, plus request details. For reporting a bug.</summary>
    Detailed,
}
