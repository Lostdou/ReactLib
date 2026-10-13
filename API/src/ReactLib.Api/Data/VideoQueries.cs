using Dapper;
using ReactLib.Api.Model;
using ReactLib.Api.Shared;

namespace ReactLib.Api.Data;

public enum GridSort { New, Top }

public sealed record GridRequest(GridSort Sort, string? Tag, string? Query, string? Cursor, int Size);

public sealed class VideoQueries(ISqlRunner sql, string copyFormat)
{
    private const int MinSize = 1;
    private const int MaxSize = 48;
    private const string PosterHost = "https://pbs.twimg.com/";
    private const string VideoHost = "https://video.twimg.com/";

    private const string CardColumns = """
        PubSeq, TweetId, MediaIndex, AuthorHandle, Title, TagsCsv,
               PosterPath, Mp4Path, DurationMs, Width, Height, CopyCount
        """;

    private const string JoinedCardColumns = """
        v.PubSeq, v.TweetId, v.MediaIndex, v.AuthorHandle, v.Title, v.TagsCsv,
               v.PosterPath, v.Mp4Path, v.DurationMs, v.Width, v.Height, v.CopyCount
        """;

    public async Task<GridPage> GetGridAsync(GridRequest request, CancellationToken ct)
    {
        var size = Math.Clamp(request.Size, MinSize, MaxSize);
        var terms = SearchTerms.From(request.Query);

        var rows = request.Sort switch
        {
            GridSort.Top => await TopAsync(size, request.Cursor, terms, ct),
            _ when !string.IsNullOrEmpty(request.Tag) => await ByTagAsync(size, request.Tag, request.Cursor, terms, ct),
            _ => await NewAsync(size, request.Cursor, terms, ct)
        };

        var items = new VideoCard[rows.Count];
        for (var i = 0; i < rows.Count; i++) items[i] = ToCard(rows[i]);

        return new GridPage(items, NextCursor(rows, size, request.Sort));
    }

    public async Task<VideoCard?> GetBySeqAsync(int seq, CancellationToken ct)
    {
        var row = await sql.QuerySingleOrDefaultAsync<VideoRow>($"""
            SELECT TOP (1) {CardColumns}
            FROM   dbo.Video
            WHERE  Status = 1 AND PubSeq = @seq
            """, new { seq }, ct);

        return row is null ? null : ToCard(row);
    }

    private Task<IReadOnlyList<VideoRow>> NewAsync(int size, string? cursor, SearchTerms terms, CancellationToken ct)
    {
        var after = Cursor.DecodeSeq(cursor);

        return sql.QueryAsync<VideoRow>($"""
            SELECT TOP (@size) {CardColumns}
            FROM   dbo.Video
            WHERE  Status = 1 AND PubSeq < @after{terms.Clause("SearchBlob")}
            ORDER BY PubSeq DESC
            """, terms.Bind(new DynamicParameters(new { size, after })), ct);
    }

    private async Task<IReadOnlyList<VideoRow>> ByTagAsync(int size, string tag, string? cursor, SearchTerms terms, CancellationToken ct)
    {
        var tagId = await FindTagIdAsync(tag, ct);
        if (tagId is null) return [];

        var after = Cursor.DecodeSeq(cursor);

        return await sql.QueryAsync<VideoRow>($"""
            SELECT TOP (@size) {JoinedCardColumns}
            FROM   dbo.VideoTag vt
            INNER JOIN dbo.Video v ON v.VideoId = vt.VideoId
            WHERE  vt.TagId = @tagId AND vt.PubSeq < @after{terms.Clause("v.SearchBlob")}
            ORDER BY vt.PubSeq DESC
            """, terms.Bind(new DynamicParameters(new { size, tagId, after })), ct);
    }

    private Task<IReadOnlyList<VideoRow>> TopAsync(int size, string? cursor, SearchTerms terms, CancellationToken ct)
    {
        var (afterScore, afterSeq) = Cursor.DecodeScore(cursor);

        return sql.QueryAsync<VideoRow>($"""
            SELECT TOP (@size) {CardColumns}, CopyCount7d
            FROM   dbo.Video
            WHERE  Status = 1
                   AND (CopyCount7d < @afterScore
                        OR (CopyCount7d = @afterScore AND PubSeq < @afterSeq)){terms.Clause("SearchBlob")}
            ORDER BY CopyCount7d DESC, PubSeq DESC
            """, terms.Bind(new DynamicParameters(new { size, afterScore, afterSeq })), ct);
    }

    private Task<short?> FindTagIdAsync(string tag, CancellationToken ct) =>
        sql.QuerySingleOrDefaultAsync<short?>(
            "SELECT TagId FROM dbo.Tag WHERE NameNorm = @slug",
            new { slug = new DbString { Value = TextNorm.Slug(tag), IsAnsi = true, Length = 30 } },
            ct);

    private static string? NextCursor(IReadOnlyList<VideoRow> rows, int size, GridSort sort)
    {
        if (rows.Count < size) return null;

        var last = rows[^1];
        return sort == GridSort.Top
            ? Cursor.Encode(last.CopyCount7d, last.PubSeq)
            : Cursor.Encode(last.PubSeq);
    }

    private VideoCard ToCard(VideoRow row)
    {
        var tweet = new TweetRef(row.AuthorHandle, row.TweetId, row.MediaIndex);

        return new VideoCard(
            row.PubSeq,
            row.AuthorHandle,
            row.Title,
            row.TagsCsv is { Length: > 0 } tags ? tags.Split(',') : [],
            copyFormat == CopyFormats.Tweet ? tweet.TweetUrl : tweet.MediaUrl,
            tweet.TweetUrl,
            row.PosterPath is null ? null : PosterHost + row.PosterPath,
            row.Mp4Path is null ? null : VideoHost + row.Mp4Path,
            row.DurationMs,
            row.Width,
            row.Height,
            row.CopyCount);
    }
}

public static class CopyFormats
{
    public const string Media = "media";
    public const string Tweet = "tweet";
}

public sealed class VideoRow
{
    public int PubSeq { get; init; }
    public long TweetId { get; init; }
    public byte MediaIndex { get; init; }
    public string AuthorHandle { get; init; } = "";
    public string Title { get; init; } = "";
    public string? TagsCsv { get; init; }
    public string? PosterPath { get; init; }
    public string? Mp4Path { get; init; }
    public int? DurationMs { get; init; }
    public short? Width { get; init; }
    public short? Height { get; init; }
    public int CopyCount { get; init; }
    public int CopyCount7d { get; init; }
}
