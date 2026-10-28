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

    /// <summary>
    /// The labels of the card sources.
    /// </summary>
    public static IReadOnlyList<SegmentOption<CardSourceKind>> CardSourceOptions { get; } =
    [
        new(CardSourceKind.Riftcodex, "Riftcodex"),
        new(CardSourceKind.RiftHunt, "RiftHunt"),
    ];

    /// <summary>
    /// What to know about a card source, for the note under the setting.
    /// </summary>
    /// <param name="source">The card source.</param>
    public static string Describe(CardSourceKind source) => source switch
    {
        CardSourceKind.RiftHunt => "RiftHunt serves their whole catalog as one download. At their request, users will only be allowed to download the card catalog once per day to avoid stress on their service.",
        _ => "Riftcodex is fetched page by page, which can take a few minutes.",
    };

    /// <summary>
    /// Says which time zone a time is in, for a time that would otherwise be taken to be the reader's own: the offset
    /// from UTC and the zone's name, such as "UTC-07:00, Pacific Daylight Time". It's the zone of the computer running
    /// Riftcaster, which isn't necessarily the one of whoever reads the page.
    /// </summary>
    /// <param name="moment">The time, since the offset (and daylight saving) depends on the date.</param>
    /// <param name="zone">The zone.</param>
    public static string ZoneLabel(DateTimeOffset moment, TimeZoneInfo zone)
    {
        var offset = zone.GetUtcOffset(moment);
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var utc = $"UTC{sign}{Math.Abs(offset.Hours):00}:{Math.Abs(offset.Minutes):00}";
        var name = zone.IsDaylightSavingTime(moment) ? zone.DaylightName : zone.StandardName;
        return string.IsNullOrWhiteSpace(name) ? utc : $"{utc}, {name}";
    }

    /// <summary>
    /// When a card source may be asked for its catalog again, in words: "tomorrow after 9:42 AM UTC-07:00, Pacific
    /// Daylight Time", with the time zone said, since the time is the server computer's.
    /// </summary>
    /// <param name="notBefore">The earliest time it may be asked.</param>
    /// <param name="now">The time now.</param>
    /// <param name="zone">The time zone to say the time in: the computer running Riftcaster's, unless a test says otherwise.</param>
    public static string When(DateTimeOffset notBefore, DateTimeOffset now, TimeZoneInfo? zone = null)
    {
        zone ??= TimeZoneInfo.Local;
        var at = TimeZoneInfo.ConvertTime(notBefore, zone);
        var today = TimeZoneInfo.ConvertTime(now, zone).Date;
        var time = $"{at.ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture)} {ZoneLabel(notBefore, zone)}";

        return at.Date <= today ? $"today after {time}"
            : at.Date == today.AddDays(1) ? $"tomorrow after {time}"
            : $"on {at.ToString("MMM d", System.Globalization.CultureInfo.InvariantCulture)} after {time}";
    }

    /// <summary>
    /// When a card source may be asked for its catalog again, as a sentence: "Available again tomorrow after 9:42 AM
    /// UTC-07:00, Pacific Daylight Time".
    /// </summary>
    /// <param name="notBefore">The earliest time it may be asked.</param>
    /// <param name="now">The time now.</param>
    /// <param name="zone">The time zone to say the time in: the computer running Riftcaster's, unless a test says otherwise.</param>
    public static string AvailableAgain(DateTimeOffset notBefore, DateTimeOffset now, TimeZoneInfo? zone = null) =>
        $"Available again {When(notBefore, now, zone)}";

    /// <summary>
    /// A moment as a date and time with its time zone said: "Oct 26, 2026 at 9:00 AM UTC-07:00, Pacific Daylight Time".
    /// </summary>
    /// <param name="moment">The moment.</param>
    /// <param name="zone">The time zone to say it in: the computer running Riftcaster's, unless a test says otherwise.</param>
    public static string Timestamp(DateTimeOffset moment, TimeZoneInfo? zone = null)
    {
        zone ??= TimeZoneInfo.Local;
        return $"{TimeZoneInfo.ConvertTime(moment, zone).ToString("MMM d, yyyy 'at' h:mm tt", System.Globalization.CultureInfo.InvariantCulture)} {ZoneLabel(moment, zone)}";
    }
}
