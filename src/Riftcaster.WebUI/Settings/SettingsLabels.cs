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

    /// <summary>
    /// The labels of the update modes.
    /// </summary>
    public static IReadOnlyList<SegmentOption<UpdateMode>> UpdateModeOptions { get; } =
    [
        new(UpdateMode.Automatic, "Automatic"),
        new(UpdateMode.AskFirst, "Ask first"),
        new(UpdateMode.Off, "Off"),
    ];

    /// <summary>
    /// What an update mode does, for the line under the setting.
    /// </summary>
    /// <param name="mode">The update mode.</param>
    public static string Describe(UpdateMode mode) => mode switch
    {
        UpdateMode.AskFirst => "Downloads quietly, but installs only when you choose Restart to update.",
        UpdateMode.Off => "Never checks or installs. Riftcaster stays on this version, even if an update was already downloaded.",
        _ => "Downloads quietly and installs when Riftcaster quits, never in the middle of a show.",
    };

    /// <summary>
    /// The labels of the update channels.
    /// </summary>
    public static IReadOnlyList<SegmentOption<UpdateChannel>> UpdateChannelOptions { get; } =
    [
        new(UpdateChannel.Stable, "Stable"),
        new(UpdateChannel.PreRelease, "Pre-release"),
    ];

    /// <summary>
    /// What an update channel looks for, for the line under the setting.
    /// </summary>
    /// <param name="channel">The update channel.</param>
    public static string Describe(UpdateChannel channel) => channel switch
    {
        UpdateChannel.PreRelease => "Includes beta builds, which can have rough edges.",
        _ => "Release versions only.",
    };

    /// <summary>
    /// How long ago something was, in words.
    /// </summary>
    /// <param name="when">When it was.</param>
    /// <param name="now">The time now.</param>
    public static string Ago(DateTimeOffset when, DateTimeOffset now)
    {
        var elapsed = now - when;
        return elapsed.TotalMinutes < 1 ? "just now"
            : elapsed.TotalHours < 1 ? Plural((int)elapsed.TotalMinutes, "minute")
            : elapsed.TotalDays < 1 ? Plural((int)elapsed.TotalHours, "hour")
            : Plural((int)elapsed.TotalDays, "day");

        static string Plural(int count, string unit) => $"{count} {unit}{(count == 1 ? "" : "s")} ago";
    }
}
