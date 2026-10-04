using System.Globalization;

namespace Riftcaster.Core.Timer;

/// <summary>
/// Formats and parses timer values as <c>mm:ss</c>.
/// </summary>
public static class TimerFormat
{
    /// <summary>
    /// The most minutes <see cref="TryParse"/> accepts: three digits, as in the first version of
    /// this application, which kept the timer legible up to 999:59.
    /// </summary>
    public const int MaxMinutes = 999;

    /// <summary>
    /// Formats whole seconds as <c>mm:ss</c>, e.g. 3000 as "50:00" and 65 as "01:05".
    /// Minutes aren't capped at 59: 3 hours is "180:00".
    /// </summary>
    /// <param name="seconds">The number of seconds, 0 or more.</param>
    /// <returns>The formatted time.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seconds"/> is negative.</exception>
    public static string Format(int seconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(seconds);

        return string.Create(CultureInfo.InvariantCulture, $"{seconds / 60:00}:{seconds % 60:00}");
    }

    /// <summary>
    /// Parses what the operator types into "Set remaining": <c>mm:ss</c> (e.g. "12:30"),
    /// or bare minutes (e.g. "45").
    /// </summary>
    /// <param name="text">The typed text. Surrounding spaces are ignored.</param>
    /// <param name="seconds">The parsed time in seconds, or 0 if the text isn't a valid time.</param>
    /// <returns>
    /// <see langword="true"/> if the text is a valid time and <see langword="false"/> otherwise
    /// (empty, not digits, signed, seconds of 60 or more, or more than <see cref="MaxMinutes"/>).
    /// </returns>
    public static bool TryParse(string? text, out int seconds)
    {
        seconds = 0;
        var parts = text?.Trim().Split(':');

        if (parts is [var bareText] && TryParseMinutes(bareText, out var bareMinutes))
        {
            seconds = bareMinutes * 60;
            return true;
        }

        if (parts is [var minutesText, var secondsText]
            && TryParseMinutes(minutesText, out var minutes)
            && TryParseNumber(secondsText, out var secs)
            && secs < 60)
        {
            seconds = minutes * 60 + secs;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Parses text as <see cref="TryParse"/> does, and says why it isn't a time when it isn't.
    /// </summary>
    /// <param name="text">The typed text. Surrounding spaces are ignored.</param>
    /// <param name="seconds">The parsed time in seconds, or 0 if the text isn't a valid time.</param>
    /// <returns><see cref="TimeEntryError.None"/> for a time, otherwise what's wrong with it.</returns>
    public static TimeEntryError Check(string? text, out int seconds)
    {
        if (TryParse(text, out seconds))
        {
            return TimeEntryError.None;
        }

        var trimmed = text?.Trim() ?? "";
        if (trimmed.StartsWith('-') && IsTimeShaped(trimmed[1..]))
        {
            return TimeEntryError.Negative;
        }

        // Shaped like a time, so only the minutes can be at fault: there are more than MaxMinutes.
        return IsTimeShaped(trimmed) ? TimeEntryError.TooLong : TimeEntryError.Format;
    }

    // Whole minutes, or minutes and seconds under 60, however many minutes.
    private static bool IsTimeShaped(string text) =>
        text.Trim().Split(':') switch
        {
            [var minutes] => IsDigits(minutes),
            [var minutes, var secs] => IsDigits(minutes) && TryParseNumber(secs, out var parsed) && parsed < 60,
            _ => false,
        };

    private static bool IsDigits(string text) => text.Length > 0 && text.All(char.IsAsciiDigit);

    private static bool TryParseMinutes(string text, out int minutes) =>
        TryParseNumber(text, out minutes) && minutes <= MaxMinutes;

    // Digits only: NumberStyles.None rejects signs, spaces, decimals and thousands separators.
    private static bool TryParseNumber(string text, out int number) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number);
}
