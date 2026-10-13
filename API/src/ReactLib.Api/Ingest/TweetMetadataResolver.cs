using System.Net.Http.Json;
using ReactLib.Api.Model;
using ReactLib.Api.Shared;

namespace ReactLib.Api.Ingest;

public interface ITweetMetadataResolver
{
    Task<TweetMetadata?> ResolveAsync(TweetRef tweet, CancellationToken ct);
}

public sealed class TweetMetadataResolver : ITweetMetadataResolver
{
    public const string PosterHost = "https://pbs.twimg.com/";
    public const string VideoHost = "https://video.twimg.com/";

    private const int FailuresBeforeOpen = 3;
    private static readonly TimeSpan BreakerCooldown = TimeSpan.FromMinutes(5);

    private readonly ILogger<TweetMetadataResolver> _log;
    private readonly Mirror[] _mirrors;

    public TweetMetadataResolver(HttpClient http, ILogger<TweetMetadataResolver> log)
    {
        _log = log;
        _mirrors =
        [
            new Mirror("fxtwitter", async (tweet, ct) => MapFx(tweet, await http.GetFromJsonAsync(
                $"https://api.fxtwitter.com/status/{tweet.TweetId}", AppJson.Default.FxResponse, ct))),

            new Mirror("vxtwitter", async (tweet, ct) => MapVx(tweet, await http.GetFromJsonAsync(
                $"https://api.vxtwitter.com/status/{tweet.TweetId}", AppJson.Default.VxResponse, ct))),
        ];
    }

    public async Task<TweetMetadata?> ResolveAsync(TweetRef tweet, CancellationToken ct)
    {
        foreach (var mirror in _mirrors)
        {
            if (mirror.IsOpen) continue;

            try
            {
                var metadata = await mirror.Fetch(tweet, ct);
                if (metadata is null) continue;

                mirror.RecordSuccess();
                return metadata;
            }
            // el timeout de HttpClient llega como TaskCanceledException: solo propagamos la
            // cancelacion real del request, si no un mirror lento devuelve 500 en vez de 400
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                _log.LogWarning(ex, "{Mirror} no resolvió {TweetId}", mirror.Name, tweet.TweetId);
                mirror.RecordFailure();
            }
        }

        return null;
    }

    private static TweetMetadata? MapFx(TweetRef tweet, FxResponse? response)
    {
        var videos = response?.Tweet?.Media?.Videos;
        if (videos is null || videos.Length == 0) return null;

        var video = videos[MediaSlot(tweet, videos.Length)];

        return new TweetMetadata(
            response!.Tweet!.Author?.ScreenName ?? tweet.Handle,
            tweet.TweetId,
            tweet.MediaIndex,
            ParseMediaKey(video.Id, video.Url),
            StripHost(video.ThumbnailUrl, PosterHost),
            StripHost(video.Url, VideoHost),
            video.Duration is { } seconds ? (int)Math.Round(seconds * 1000) : null,
            (short?)video.Width,
            (short?)video.Height,
            response.Tweet.Text);
    }

    private static TweetMetadata? MapVx(TweetRef tweet, VxResponse? response)
    {
        var videos = response?.Media?.Where(m => m.Type is "video" or "gif").ToArray();
        if (videos is null || videos.Length == 0) return null;

        var video = videos[MediaSlot(tweet, videos.Length)];

        return new TweetMetadata(
            response!.ScreenName ?? tweet.Handle,
            tweet.TweetId,
            tweet.MediaIndex,
            ParseMediaKey(video.IdStr, video.Url),
            StripHost(video.ThumbnailUrl, PosterHost),
            StripHost(video.Url, VideoHost),
            video.DurationMillis,
            (short?)video.Size?.Width,
            (short?)video.Size?.Height,
            response.Text);
    }

    // MediaIndex es 1-based porque asi lo numera la URL /video/<n>
    private static int MediaSlot(TweetRef tweet, int available) => Math.Min(tweet.MediaIndex - 1, available - 1);

    private static long? ParseMediaKey(string? id, string? url) =>
        long.TryParse(id, out var mediaKey) ? mediaKey : TweetUrl.ExtractMediaKey(url);

    // guardamos el path y no la URL entera: el host lo rearma la API y ahorra bytes en tres indices
    private static string? StripHost(string? url, string host) =>
        string.IsNullOrEmpty(url) || !url.StartsWith(host, StringComparison.Ordinal)
            ? null
            : url[host.Length..];

    private sealed class Mirror(string name, Func<TweetRef, CancellationToken, Task<TweetMetadata?>> fetch)
    {
        private int _failures;
        private long _openUntil;

        public string Name => name;
        public Func<TweetRef, CancellationToken, Task<TweetMetadata?>> Fetch => fetch;
        public bool IsOpen => Interlocked.Read(ref _openUntil) > Environment.TickCount64;

        public void RecordSuccess() => Interlocked.Exchange(ref _failures, 0);

        public void RecordFailure()
        {
            if (Interlocked.Increment(ref _failures) < FailuresBeforeOpen) return;

            Interlocked.Exchange(ref _openUntil, Environment.TickCount64 + (long)BreakerCooldown.TotalMilliseconds);
            Interlocked.Exchange(ref _failures, 0);
        }
    }
}
