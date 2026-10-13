using System.Data;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.Extensions.Options;
using ReactLib.Api.Data;
using ReactLib.Api.Model;
using ReactLib.Api.Shared;

namespace ReactLib.Api.Ingest;

public enum SubmitOutcome { Published, InvalidUrl, AlreadyExists, NoVideoFound }

public sealed record SubmitResult(
    SubmitOutcome Outcome,
    VideoPreview? Preview = null,
    NearDuplicate? Duplicate = null,
    int? Seq = null,
    string[]? ProposedTags = null);

public sealed record PublishedRow(int PubSeq, string? ProposedTags);

public sealed class SubmissionService(
    ISqlRunner sql,
    ITweetMetadataResolver resolver,
    IOptions<ApiOptions> options,
    ILogger<SubmissionService> log)
{
    private const int MaxTitleLength = 80;
    private const int MaxTags = 5;
    private const int DuplicateThreshold = 8;
    private const int MinDistinctiveBits = 8;
    private const int MaxDistinctiveBits = 56;

    public async Task<SubmitResult> ResolveAsync(string url, CancellationToken ct)
    {
        if (!TweetUrl.TryParse(url, out var tweet)) return new SubmitResult(SubmitOutcome.InvalidUrl);

        if (await ExistsAsync(tweet.TweetId, tweet.MediaIndex, ct))
            return new SubmitResult(SubmitOutcome.AlreadyExists);

        var metadata = await resolver.ResolveAsync(tweet, ct);
        if (metadata?.Mp4Path is null) return new SubmitResult(SubmitOutcome.NoVideoFound);

        return new SubmitResult(SubmitOutcome.Published, ToPreview(metadata));
    }

    public async Task<SubmitResult> SubmitAsync(string url, string? tags, long? dhash, string? ip, CancellationToken ct)
    {
        if (!TweetUrl.TryParse(url, out var tweet)) return new SubmitResult(SubmitOutcome.InvalidUrl);

        var metadata = await resolver.ResolveAsync(tweet, ct);
        if (metadata?.Mp4Path is null) return new SubmitResult(SubmitOutcome.NoVideoFound);

        if (metadata.MediaKey is { } mediaKey && await MediaKeyExistsAsync(mediaKey, ct))
            return new SubmitResult(SubmitOutcome.AlreadyExists);

        var hash = IsDistinctive(dhash) ? dhash : null;
        var duplicate = hash is null ? null : await FindNearDuplicateAsync(hash.Value, ct);

        // el corte entre tags aplicados y propuestos lo decide el proc, no el caché en memoria,
        // que puede tener hasta 10 minutos de atraso y mentirle al que envía
        var published = await PublishAsync(metadata, NormalizeTags(tags), hash, ip, ct);
        if (published is null || published.PubSeq == 0) return new SubmitResult(SubmitOutcome.AlreadyExists);

        var proposed = published.ProposedTags?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [];

        log.LogInformation("publicado {Seq} ({Handle}/{TweetId})", published.PubSeq, metadata.Handle, metadata.TweetId);
        return new SubmitResult(
            SubmitOutcome.Published, ToPreview(metadata), duplicate, published.PubSeq, proposed);
    }

    public static string? NormalizeTags(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var slugs = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => TextNorm.Slug(t))
            .Where(t => t.Length > 1)
            .Distinct(StringComparer.Ordinal)
            .Take(MaxTags)
            .Order(StringComparer.Ordinal);

        var joined = string.Join(',', slugs);
        return joined.Length == 0 ? null : joined;
    }

    private static VideoPreview ToPreview(TweetMetadata metadata) => new(
        metadata.Handle,
        TitleFor(metadata),
        metadata.PosterPath is null ? null : TweetMetadataResolver.PosterHost + metadata.PosterPath,
        metadata.Mp4Path is null ? null : TweetMetadataResolver.VideoHost + metadata.Mp4Path,
        metadata.DurationMs,
        metadata.Width,
        metadata.Height);

    private static string TitleFor(TweetMetadata metadata)
    {
        var text = metadata.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return $"Video de @{metadata.Handle}";

        var firstLine = text.Split('\n', 2)[0].Trim();
        return firstLine.Length <= MaxTitleLength ? firstLine : firstLine[..(MaxTitleLength - 1)] + "…";
    }

    // un frame plano (fade a negro, pantalla blanca) matchearia contra todo el catalogo
    private static bool IsDistinctive(long? hash) =>
        hash is not null && BitOperations.PopCount((ulong)hash.Value) is >= MinDistinctiveBits and <= MaxDistinctiveBits;

    private async Task<bool> ExistsAsync(long tweetId, byte mediaIndex, CancellationToken ct) =>
        await sql.QuerySingleOrDefaultAsync<int?>(
            "SELECT VideoId FROM dbo.Video WHERE TweetId = @tweetId AND MediaIndex = @mediaIndex",
            new { tweetId, mediaIndex }, ct) is not null;

    private async Task<bool> MediaKeyExistsAsync(long mediaKey, CancellationToken ct) =>
        await sql.QuerySingleOrDefaultAsync<int?>(
            "SELECT VideoId FROM dbo.Video WHERE MediaKey = @mediaKey", new { mediaKey }, ct) is not null;

    private async Task<NearDuplicate?> FindNearDuplicateAsync(long hash, CancellationToken ct)
    {
        var matches = await sql.QueryAsync<NearDuplicate>("""
            SELECT TOP (1) PubSeq AS Seq, Title, CAST(BIT_COUNT(Dhash ^ @hash) AS tinyint) AS Distance
            FROM   dbo.Video WITH (INDEX(IX_Video_Dhash))
            WHERE  Dhash IS NOT NULL AND Status IN (0, 1) AND BIT_COUNT(Dhash ^ @hash) <= @threshold
            ORDER BY Distance, PubSeq
            """, new { hash, threshold = DuplicateThreshold }, ct);

        return matches.Count > 0 ? matches[0] : null;
    }

    private Task<PublishedRow?> PublishAsync(
        TweetMetadata metadata, string? tags, long? hash, string? ip, CancellationToken ct)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Handle", metadata.Handle, DbType.AnsiString, size: 15);
        parameters.Add("TweetId", metadata.TweetId);
        parameters.Add("Title", TitleFor(metadata), DbType.String, size: MaxTitleLength);
        parameters.Add("TagsCsv", tags, DbType.AnsiString, size: 160);
        parameters.Add("MediaIndex", metadata.MediaIndex);
        parameters.Add("PosterPath", metadata.PosterPath, DbType.AnsiString, size: 120);
        parameters.Add("Mp4Path", metadata.Mp4Path, DbType.AnsiString, size: 200);
        parameters.Add("DurationMs", metadata.DurationMs);
        parameters.Add("Width", metadata.Width);
        parameters.Add("Height", metadata.Height);
        parameters.Add("MediaKey", metadata.MediaKey);
        parameters.Add("Dhash", hash);
        parameters.Add("IpHash", HashIp(ip), DbType.Binary, size: 8);

        return sql.QuerySingleOrDefaultAsync<PublishedRow>("dbo.SubmitVideo", parameters, ct, CommandType.StoredProcedure);
    }

    // truncado a 8 bytes: alcanza para banear reincidentes sin guardar la IP en claro.
    // Sin salt no guardamos nada: el espacio IPv4 entero se recorre en minutos, asi que un
    // hash con salt conocido es una IP en claro con pasos extra
    private byte[]? HashIp(string? ip)
    {
        var salt = options.Value.IpHashSalt;
        if (string.IsNullOrEmpty(ip) || string.IsNullOrEmpty(salt)) return null;

        var digest = HMACSHA256.HashData(Encoding.UTF8.GetBytes(salt), Encoding.UTF8.GetBytes(ip));
        return digest[..8];
    }
}
