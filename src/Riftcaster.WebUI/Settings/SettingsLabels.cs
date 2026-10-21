using Riftcaster.Contracts;
using Riftcaster.WebUI.Shared;

namespace Riftcaster.WebUI.Settings;

/// <summary>
/// The words the Settings page uses for its choices.
/// </summary>
public static class SettingsLabels
{
    /// <summary>
    /// The labels of the log details.
    /// </summary>
    public static IReadOnlyList<SegmentOption<LogDetail>> LogDetailOptions { get; } =
    [
        new(LogDetail.WarningsOnly, "Warnings only"),
        new(LogDetail.Normal, "Normal"),
        new(LogDetail.Detailed, "Detailed"),
    ];

    /// <summary>
    /// What a log detail logs, for the line under the setting.
    /// </summary>
    /// <param name="detail">The log detail.</param>
    public static string Describe(LogDetail detail) => detail switch
    {
        LogDetail.WarningsOnly => "Only problems are logged. Keeps the log files smallest.",
        LogDetail.Detailed => "Adds request details. Use it when reporting a bug; the log fills up faster.",
        _ => "Startup, updates, card refreshes and warnings. Right for everyday use.",
    };
}
