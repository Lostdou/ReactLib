using System.Data;
using System.Numerics;
using Dapper;
using ReactLib.Api.Data;
using ReactLib.Api.Model;
using ReactLib.Api.Shared;

namespace ReactLib.Api.Ingest;

public enum IngestOutcome { Published, InvalidUrl, AlreadyExists, NoVideoFound }

public sealed record IngestResult(
    IngestOutcome Outcome,
    int? Seq = null,
    NearDuplicate? Duplicate = null);

public sealed class VideoIngestor(
    ISqlRunner sql,
    ITweetMetadataResolver resolver,
    ILogger<VideoIngestor> log)
{
    private const int MaxTitleLength = 80;
    private const int DuplicateThreshold = 8;
    private const int MinDistinctiveBits = 8;
    private const int MaxDistinctiveBits = 56;

    public async Task<IngestResult> IngestAsync(
        string url, string? title, string? tags, long? dhash, CancellationToken ct)
    {
        if (!TweetUrl.TryParse(url, out var tweet)) return new IngestResult(IngestOutcome.InvalidUrl);

        if (await FindExistingAsync(tweet.TweetId, tweet.MediaIndex, ct) is { } existing)
            return new IngestResult(IngestOutcome.AlreadyExists, existing);

        var metadata = await resolver.ResolveAsync(tweet, ct);
        if (metadata?.Mp4Path is null) return new IngestResult(IngestOutcome.NoVideoFound);

        if (metadata.MediaKey is { } mediaKey && await FindByMediaKeyAsync(mediaKey, ct) is { } republished)
            return new IngestResult(IngestOutcome.AlreadyExists, republished);

        // el hash lo calcula el browser sobre el poster: pbs.twimg.com habilita CORS, y asi la caja
        // de 256 MB nunca decodifica una imagen. Si no viene, la fila queda fuera del dedup perceptual
        var hash = IsDistinctive(dhash) ? dhash : null;
        var nearDuplicate = hash is null ? null : await FindNearDuplicateAsync(hash.Value, ct);

        var seq = await PublishAsync(metadata, TitleFor(metadata, title), tags, hash, ct);
        log.LogInformation("publicado {Seq} ({Handle}/{TweetId})", seq, metadata.Handle, metadata.TweetId);

        return new IngestResult(IngestOutcome.Published, seq, nearDuplicate);
    }

    // un frame plano (fade a negro, pantalla blanca) matchearia contra todo el catalogo
    private static bool IsDistinctive(long? hash)
    {
        if (hash is null) return false;

        var bits = BitOperations.PopCount((ulong)hash.Value);
        return bits is >= MinDistinctiveBits and <= MaxDistinctiveBits;
    }

    private static string TitleFor(TweetMetadata metadata, string? requested)
    {
        var title = string.IsNullOrWhiteSpace(requested) ? metadata.Text?.Trim() : requested.Trim();
        if (string.IsNullOrWhiteSpace(title)) return $"Video de @{metadata.Handle}";

        var firstLine = title.Split('\n', 2)[0].Trim();
        return firstLine.Length <= MaxTitleLength ? firstLine : firstLine[..(MaxTitleLength - 1)] + "…";
    }

    private Task<int?> FindExistingAsync(long tweetId, byte mediaIndex, CancellationToken ct) =>
        sql.QuerySingleOrDefaultAsync<int?>(
            "SELECT PubSeq FROM dbo.Video WHERE TweetId = @tweetId AND MediaIndex = @mediaIndex",
            new { tweetId, mediaIndex }, ct);

    private Task<int?> FindByMediaKeyAsync(long mediaKey, CancellationToken ct) =>
        sql.QuerySingleOrDefaultAsync<int?>(
            "SELECT PubSeq FROM dbo.Video WHERE MediaKey = @mediaKey",
            new { mediaKey }, ct);

    // BIT_COUNT en el WHERE nunca es SARGable, pero el indice filtrado son 31 B por fila:
    // escanearlo entero cuesta menos que mantener bandas que ademas pierden recall arriba de d=3
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

    private async Task<int> PublishAsync(
        TweetMetadata metadata, string title, string? tags, long? hash, CancellationToken ct)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Handle", metadata.Handle, DbType.AnsiString, size: 15);
        parameters.Add("TweetId", metadata.TweetId);
        parameters.Add("Title", title, DbType.String, size: MaxTitleLength);
        parameters.Add("Tags", tags, DbType.String, size: 160);
        parameters.Add("MediaIndex", metadata.MediaIndex);
        parameters.Add("PosterPath", metadata.PosterPath, DbType.AnsiString, size: 120);
        parameters.Add("Mp4Path", metadata.Mp4Path, DbType.AnsiString, size: 200);
        parameters.Add("DurationMs", metadata.DurationMs);
        parameters.Add("Width", metadata.Width);
        parameters.Add("Height", metadata.Height);
        parameters.Add("Dhash", hash);

        var seq = await sql.QuerySingleOrDefaultAsync<int?>("dbo.PublishVideo", parameters, ct, CommandType.StoredProcedure);
        return seq ?? 0;
    }
}

public sealed record NearDuplicate(int Seq, string Title, byte Distance);
