using System.Text.RegularExpressions;

namespace EHub.Application.Features.Showcase;

/// <summary>
/// The only video providers a showcase may point to. A student-supplied link is never trusted as-is: it is reduced to a
/// canonical YouTube or Vimeo watch URL built from the video id alone, so nothing else in the original link (host tricks,
/// extra parameters, credentials) can reach another user's browser through the feed preview.
/// </summary>
public static partial class ShowcaseVideoLinks
{
    [GeneratedRegex("^[A-Za-z0-9_-]{11}$")]
    private static partial Regex YouTubeId();

    [GeneratedRegex("^[0-9]{1,12}$")]
    private static partial Regex VimeoId();

    public static bool IsSupported(string? value) => Normalize(value) is not null;

    /// <summary>Returns the canonical watch URL for a supported https YouTube or Vimeo link, otherwise null.</summary>
    public static string? Normalize(string? value)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo)) return null;

        var host = uri.Host.ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal)) host = host[4..];
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        string? youTubeId = host switch
        {
            "youtu.be" when segments.Length == 1 => segments[0],
            "youtube.com" or "m.youtube.com" when segments is ["watch"] => QueryValue(uri.Query, "v"),
            "youtube.com" or "m.youtube.com" when segments is ["shorts" or "embed", _] => segments[1],
            _ => null
        };
        if (youTubeId is not null && YouTubeId().IsMatch(youTubeId)) return $"https://www.youtube.com/watch?v={youTubeId}";

        if (host == "vimeo.com" && segments.Length == 1 && VimeoId().IsMatch(segments[0])) return $"https://vimeo.com/{segments[0]}";
        return null;
    }

    private static string? QueryValue(string query, string name)
    {
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts[0] == name && parts.Length == 2) return Uri.UnescapeDataString(parts[1]);
        }
        return null;
    }
}
