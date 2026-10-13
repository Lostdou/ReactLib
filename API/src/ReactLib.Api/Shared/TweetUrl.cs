using System.Text.RegularExpressions;

namespace ReactLib.Api.Shared;

public readonly record struct TweetRef(string Handle, long TweetId, byte MediaIndex)
{
    public string TweetUrl => $"https://x.com/{Handle}/status/{TweetId}";

    public string MediaUrl => $"{TweetUrl}/video/{MediaIndex}";
}

public static partial class TweetUrl
{
    private const int MaxUrlLength = 300;
    private const byte FirstMedia = 1;

    // misma expresion que Web/src/app/core/tweet-url.ts: si cambia una, cambian las dos
    [GeneratedRegex(
        @"^https?://(?:www\.)?(?:x|twitter)\.com/(?<u>[A-Za-z0-9_]{1,15})/status/(?<id>\d{1,19})(?:/(?:video|photo)/(?<n>[1-4]))?",
        RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant)]
    private static partial Regex TweetPattern { get; }

    [GeneratedRegex(@"/(?:ext_tw_video|amplify_video)/(?<mid>\d{1,19})/",
        RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant)]
    private static partial Regex MediaKeyPattern { get; }

    public static bool TryParse(string? url, out TweetRef result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(url) || url.Length > MaxUrlLength) return false;

        var match = TweetPattern.Match(url.Trim());
        if (!match.Success) return false;
        if (!long.TryParse(match.Groups["id"].ValueSpan, out var tweetId) || tweetId <= 0) return false;

        var media = match.Groups["n"];
        result = new TweetRef(
            match.Groups["u"].Value,
            tweetId,
            media.Success ? byte.Parse(media.ValueSpan) : FirstMedia);

        return true;
    }

    public static long? ExtractMediaKey(string? mp4Path)
    {
        if (string.IsNullOrEmpty(mp4Path)) return null;

        var match = MediaKeyPattern.Match(mp4Path);
        return match.Success && long.TryParse(match.Groups["mid"].ValueSpan, out var mediaKey)
            ? mediaKey
            : null;
    }
}
