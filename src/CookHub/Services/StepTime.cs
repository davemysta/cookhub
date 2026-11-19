using System.Globalization;
using System.Text.RegularExpressions;

namespace CookHub.Services;

// A point in a recipe's video, entered and shown as minutes and seconds ("01:30").
public static partial class StepTime
{
    public const string Placeholder = "00:00";

    // Longer than any recipe video is likely to be, and well within an int.
    public const int MaxSeconds = (999 * 60) + 59;

    // Returns the number of seconds, or null when the text is blank or not in mm:ss form.
    public static int? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = Pattern().Match(text.Trim());
        if (!match.Success)
        {
            return null;
        }

        var minutes = int.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture);
        var seconds = int.Parse(match.Groups["s"].Value, CultureInfo.InvariantCulture);
        return (minutes * 60) + seconds;
    }

    // True when the text is blank (no time) or a valid mm:ss time.
    public static bool IsValid(string? text) => string.IsNullOrWhiteSpace(text) || Parse(text) is not null;

    public static string Format(int seconds)
    {
        seconds = Math.Clamp(seconds, 0, MaxSeconds);
        return string.Create(CultureInfo.InvariantCulture, $"{seconds / 60:00}:{seconds % 60:00}");
    }

    public static string? Format(int? seconds) => seconds is { } s ? Format(s) : null;

    [GeneratedRegex("^(?<m>[0-9]{1,3}):(?<s>[0-5][0-9])$")]
    private static partial Regex Pattern();
}
