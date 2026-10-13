using System.Text.Json.Serialization;
using ReactLib.Api.Endpoints;
using ReactLib.Api.Ingest;

namespace ReactLib.Api.Model;

public sealed record VideoCard(
    int Seq,
    string Handle,
    string Title,
    string[] Tags,
    string CopyUrl,
    string TweetUrl,
    string? PosterUrl,
    string? Mp4Url,
    int? DurationMs,
    short? Width,
    short? Height,
    int Copies);

public sealed record GridPage(IReadOnlyList<VideoCard> Items, string? Next);

// CopyFormat es un flag para cambiar el boton copiar sin redeploy del front el dia que X rompa /video/N
public sealed record ApiConfig(string CopyFormat, int CatalogVersion);

public sealed record HealthInfo(string Status, string Version, long UptimeSeconds);

public sealed record IngestResponse(
    string Outcome,
    int? Seq,
    NearDuplicate? PossibleDuplicate);

public sealed record VideoPreview(
    string Handle,
    string Title,
    string? PosterUrl,
    string? Mp4Url,
    int? DurationMs,
    short? Width,
    short? Height);

public sealed record SubmitResponse(
    string Outcome,
    VideoPreview? Preview,
    NearDuplicate? PossibleDuplicate,
    int? Seq,
    string[]? ProposedTags);

public sealed record ProposedTag(string Slug, int Videos);

public sealed record PendingItem(
    int VideoId,
    string Handle,
    long TweetId,
    byte MediaIndex,
    string Title,
    string? TagsCsv,
    string? PosterUrl,
    string? Mp4Url,
    short? Width,
    short? Height,
    int? DupOf,
    byte? DupDist,
    DateTime CreatedUtc);

public sealed record MemoryInfo(
    long ManagedBytes,
    long CommittedBytes,
    long PrivateBytes,
    long SqlCommands,
    int Gen0,
    int Gen1,
    int Gen2);

// el dhash son 64 bits y viaja como string: un number de JS pierde precision arriba de 2^53
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(VideoCard))]
[JsonSerializable(typeof(GridPage))]
[JsonSerializable(typeof(ApiConfig))]
[JsonSerializable(typeof(HealthInfo))]
[JsonSerializable(typeof(MemoryInfo))]
[JsonSerializable(typeof(FxResponse))]
[JsonSerializable(typeof(VxResponse))]
[JsonSerializable(typeof(IngestResponse))]
[JsonSerializable(typeof(IngestRequest))]
[JsonSerializable(typeof(VideoPreview))]
[JsonSerializable(typeof(SubmitResponse))]
[JsonSerializable(typeof(SubmitRequest))]
[JsonSerializable(typeof(ResolveRequest))]
[JsonSerializable(typeof(ApproveRequest))]
[JsonSerializable(typeof(TagSuggestion[]))]
[JsonSerializable(typeof(PendingItem[]))]
[JsonSerializable(typeof(ProposedTag[]))]
public sealed partial class AppJson : JsonSerializerContext;
