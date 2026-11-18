using System.Globalization;
using System.Text.RegularExpressions;

namespace CookHub.Services;

public enum VideoEmbedKind
{
    // A YouTube or Vimeo player page, shown in an iframe.
    Player,

    // A video file, played with the browser's own <video> element.
    File,
}

// Turns a video link a user pasted into something the recipe page can embed.
// Only links that parse into a known, safe form are accepted, so arbitrary pages are never framed.
public sealed partial record VideoEmbed(VideoEmbedKind Kind, string Src)
{
    public const string SupportedDescription = "a YouTube or Vimeo link, or a link to an MP4, WebM or Ogg video file";

    private static readonly string[] FileExtensions = [".mp4", ".m4v", ".webm", ".ogv", ".ogg"];

    public static VideoEmbed? Parse(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)
            || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return null;
        }

        var host = uri.Host.ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            host = host[4..];
        }
        else if (host.StartsWith("m.", StringComparison.Ordinal))
        {
            host = host[2..];
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var query = ParseQuery(uri.Query);

        return host switch
        {
            "youtube.com" or "youtube-nocookie.com" or "music.youtube.com" => YouTube(YouTubeIdFromPath(segments, query), query),
            "youtu.be" => YouTube(segments.FirstOrDefault(), query),
            "vimeo.com" => Vimeo(segments, query),
            "player.vimeo.com" when segments is ["video", var id, ..] => Vimeo([id], query),
            _ => VideoFile(uri),
        };
    }

    private static string? YouTubeIdFromPath(string[] segments, Dictionary<string, string> query) => segments switch
    {
        ["watch"] => query.GetValueOrDefault("v"),
        ["embed" or "shorts" or "live" or "v", var id, ..] => id,
        _ => null,
    };

    private static VideoEmbed? YouTube(string? id, Dictionary<string, string> query)
    {
        if (id is null || !YouTubeId().IsMatch(id))
        {
            return null;
        }

        var src = $"https://www.youtube-nocookie.com/embed/{id}";
        var start = ParseSeconds(query.GetValueOrDefault("t") ?? query.GetValueOrDefault("start"));
        return new VideoEmbed(VideoEmbedKind.Player, start > 0 ? $"{src}?start={start}" : src);
    }

    private static VideoEmbed? Vimeo(string[] segments, Dictionary<string, string> query)
    {
        // vimeo.com/123456, vimeo.com/123456/abcdef (unlisted), vimeo.com/channels/name/123456
        var index = Array.FindIndex(segments, s => VimeoId().IsMatch(s));
        if (index < 0)
        {
            return null;
        }

        var id = segments[index];
        var hash = index + 1 < segments.Length && VimeoHash().IsMatch(segments[index + 1])
            ? segments[index + 1]
            : query.GetValueOrDefault("h");
        var src = $"https://player.vimeo.com/video/{id}";
        return new VideoEmbed(VideoEmbedKind.Player, hash is not null && VimeoHash().IsMatch(hash) ? $"{src}?h={hash}" : src);
    }

    private static VideoEmbed? VideoFile(Uri uri) =>
        FileExtensions.Any(e => uri.AbsolutePath.EndsWith(e, StringComparison.OrdinalIgnoreCase))
            ? new VideoEmbed(VideoEmbedKind.File, uri.AbsoluteUri)
            : null;

    // Accepts "90", "90s" and "1h2m3s" forms.
    private static int ParseSeconds(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

        if (int.TryParse(value.TrimEnd('s'), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
        {
            return seconds;
        }

        var match = Duration().Match(value);
        if (!match.Success)
        {
            return 0;
        }

        static int Part(Group g) => g.Success ? int.Parse(g.Value, CultureInfo.InvariantCulture) : 0;
        return (Part(match.Groups["h"]) * 3600) + (Part(match.Groups["m"]) * 60) + Part(match.Groups["s"]);
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            result.TryAdd(Uri.UnescapeDataString(parts[0]), parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "");
        }

        return result;
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{11}$")]
    private static partial Regex YouTubeId();

    [GeneratedRegex("^[0-9]{1,12}$")]
    private static partial Regex VimeoId();

    [GeneratedRegex("^[0-9a-f]{6,32}$")]
    private static partial Regex VimeoHash();

    [GeneratedRegex("^(?:(?<h>[0-9]{1,3})h)?(?:(?<m>[0-9]{1,4})m)?(?:(?<s>[0-9]{1,5})s)?$")]
    private static partial Regex Duration();
}
