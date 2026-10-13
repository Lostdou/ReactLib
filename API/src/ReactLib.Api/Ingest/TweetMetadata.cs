using System.Text.Json.Serialization;

namespace ReactLib.Api.Ingest;

public sealed record TweetMetadata(
    string Handle,
    long TweetId,
    byte MediaIndex,
    long? MediaKey,
    string? PosterPath,
    string? Mp4Path,
    int? DurationMs,
    short? Width,
    short? Height,
    string? Text);

public sealed record FxResponse(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("tweet")] FxTweet? Tweet);

public sealed record FxTweet(
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("author")] FxAuthor? Author,
    [property: JsonPropertyName("media")] FxMedia? Media);

public sealed record FxAuthor(
    [property: JsonPropertyName("screen_name")] string? ScreenName);

public sealed record FxMedia(
    [property: JsonPropertyName("videos")] FxVideo[]? Videos);

public sealed record FxVideo(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("thumbnail_url")] string? ThumbnailUrl,
    [property: JsonPropertyName("duration")] double? Duration,
    [property: JsonPropertyName("width")] int? Width,
    [property: JsonPropertyName("height")] int? Height);

public sealed record VxResponse(
    [property: JsonPropertyName("user_screen_name")] string? ScreenName,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("media_extended")] VxMedia[]? Media);

public sealed record VxMedia(
    [property: JsonPropertyName("id_str")] string? IdStr,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("thumbnail_url")] string? ThumbnailUrl,
    [property: JsonPropertyName("duration_millis")] int? DurationMillis,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("size")] VxSize? Size);

public sealed record VxSize(
    [property: JsonPropertyName("width")] int? Width,
    [property: JsonPropertyName("height")] int? Height);
