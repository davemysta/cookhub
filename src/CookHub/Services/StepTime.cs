using System.Globalization;
using System.Text.RegularExpressions;

namespace CookHub.Services;

// A point in a recipe's video, shown as minutes and seconds ("01:30"). It can be typed as mm:ss,
// or as just digits where the last two are the seconds: "1" is 00:01, "209" is 02:09, "1423" is 14:23.
public static partial class StepTime
{
    public const string Placeholder = "00:00";

    // Longer than any recipe video is likely to be, and well within an int.
    public const int MaxSeconds = (999 * 60) + 59;

    // Returns the number of seconds, or null when the text is blank or isn't a time in either form.
    public static int? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        text = text.Trim();
        var match = Pattern().Match(text);
        if (match.Success)
        {
            return (ParseInt(match.Groups["m"].Value) * 60) + ParseInt(match.Groups["s"].Value);
        }

        if (DigitsPattern().IsMatch(text))
        {
            var split = Math.Max(0, text.Length - 2);
            var minutes = split == 0 ? 0 : ParseInt(text[..split]);
            var seconds = ParseInt(text[split..]);
            return seconds < 60 ? (minutes * 60) + seconds : null;
        }

        return null;
    }

    // True when the text is blank (no time) or a valid time.
    public static bool IsValid(string? text) => string.IsNullOrWhiteSpace(text) || Parse(text) is not null;

    // Rewrites a valid time as mm:ss ("209" becomes "02:09"). Blank becomes null, and anything
    // that isn't a time is left as typed so it can be shown as an error and corrected.
    public static string? Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return Parse(text) is { } seconds ? Format(seconds) : text;
    }

    public static string Format(int seconds)
    {
        seconds = Math.Clamp(seconds, 0, MaxSeconds);
        return string.Create(CultureInfo.InvariantCulture, $"{seconds / 60:00}:{seconds % 60:00}");
    }

    public static string? Format(int? seconds) => seconds is { } s ? Format(s) : null;

    private static int ParseInt(string digits) => int.Parse(digits, CultureInfo.InvariantCulture);

    [GeneratedRegex("^(?<m>[0-9]{1,3}):(?<s>[0-5][0-9])$")]
    private static partial Regex Pattern();

    // Up to five digits: the last two (or the only one or two) are seconds, any before them minutes.
    [GeneratedRegex("^[0-9]{1,5}$")]
    private static partial Regex DigitsPattern();
}
