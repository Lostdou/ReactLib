using System.Data;
using Microsoft.Extensions.Options;
using ReactLib.Api.Data;
using ReactLib.Api.Ingest;
using ReactLib.Api.Model;
using ReactLib.Api.Shared;

namespace ReactLib.Api.Endpoints;

public sealed record IngestRequest(string Url, string? Title, string? Tags, long? Dhash);

public sealed record ApproveRequest(string? Title, string? Tags);

public static class AdminEndpoints
{
    private const int QueuePageSize = 30;

    public static void MapAdmin(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin").AddEndpointFilter(RequireAdminKey);

        admin.MapPost("/videos", Ingest);
        admin.MapGet("/queue", Queue);
        admin.MapPost("/queue/{videoId:int}/approve", Approve);
        admin.MapPost("/queue/{videoId:int}/reject", Reject);
        admin.MapGet("/proposed-tags", ProposedTags);
        admin.MapPost("/proposed-tags/{slug}/approve", ApproveTag);
        admin.MapPost("/proposed-tags/{slug}/reject", RejectTag);
    }

    private static async Task<IResult> ProposedTags(ISqlRunner sql, CancellationToken ct)
    {
        var rows = await sql.QueryAsync<ProposedTag>("""
            SELECT   s.value AS Slug, COUNT(*) AS Videos
            FROM     dbo.Video v
            CROSS APPLY STRING_SPLIT(v.ProposedTags, ',') s
            WHERE    v.ProposedTags IS NOT NULL AND s.value <> ''
            GROUP BY s.value
            ORDER BY COUNT(*) DESC, s.value
            """, null, ct);

        return Results.Ok(rows.ToArray());
    }

    private static async Task<IResult> ApproveTag(
        string slug, ISqlRunner sql, TagCatalog tags, CancellationToken ct)
    {
        var normalized = TextNorm.Slug(slug);
        if (normalized.Length < 2) return Results.BadRequest();

        await sql.QuerySingleOrDefaultAsync<short?>(
            "dbo.ApproveProposedTag", new { Slug = normalized }, ct, CommandType.StoredProcedure);

        await tags.RefreshAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> RejectTag(string slug, ISqlRunner sql, CancellationToken ct)
    {
        await sql.ExecuteAsync(
            "dbo.RejectProposedTag", new { Slug = TextNorm.Slug(slug) }, ct, CommandType.StoredProcedure);

        return Results.NoContent();
    }

    private static async Task<IResult> Ingest(IngestRequest request, VideoIngestor ingestor, CancellationToken ct)
    {
        var result = await ingestor.IngestAsync(request.Url, request.Title, request.Tags, request.Dhash, ct);
        var body = new IngestResponse(result.Outcome.ToString(), result.Seq, result.Duplicate);

        return result.Outcome switch
        {
            IngestOutcome.Published => Results.Ok(body),
            IngestOutcome.AlreadyExists => Results.Conflict(body),
            _ => Results.BadRequest(body),
        };
    }

    private static async Task<IResult> Queue(ISqlRunner sql, CancellationToken ct)
    {
        var rows = await sql.QueryAsync<PendingRow>($"""
            SELECT TOP ({QueuePageSize})
                   VideoId, AuthorHandle, TweetId, MediaIndex, Title, TagsCsv,
                   PosterPath, Mp4Path, Width, Height, DupOf, DupDist, CreatedUtc
            FROM   dbo.Video
            WHERE  Status = 0
            ORDER BY CreatedUtc
            """, null, ct);

        return Results.Ok(rows.Select(r => new PendingItem(
            r.VideoId, r.AuthorHandle, r.TweetId, r.MediaIndex, r.Title, r.TagsCsv,
            r.PosterPath is null ? null : TweetMetadataResolver.PosterHost + r.PosterPath,
            r.Mp4Path is null ? null : TweetMetadataResolver.VideoHost + r.Mp4Path,
            r.Width, r.Height, r.DupOf, r.DupDist, r.CreatedUtc)).ToArray());
    }

    private static async Task<IResult> Approve(
        int videoId, ApproveRequest request, ISqlRunner sql, CancellationToken ct)
    {
        var seq = await sql.QuerySingleOrDefaultAsync<int>("dbo.ApproveSubmission", new
        {
            VideoId = videoId,
            request.Title,
            Tags = SubmissionService.NormalizeTags(request.Tags),
        }, ct, CommandType.StoredProcedure);

        return seq > 0 ? Results.Ok(new { seq }) : Results.NotFound();
    }

    private static async Task<IResult> Reject(int videoId, ISqlRunner sql, CancellationToken ct)
    {
        var affected = await sql.QuerySingleOrDefaultAsync<int>(
            "dbo.RejectVideo", new { VideoId = videoId }, ct, CommandType.StoredProcedure);

        return affected > 0 ? Results.NoContent() : Results.NotFound();
    }

    // 404 y no 401: la superficie de admin no tiene por que ser descubrible escaneando
    private static async ValueTask<object?> RequireAdminKey(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var expected = context.HttpContext.RequestServices
            .GetRequiredService<IOptions<ApiOptions>>().Value.AdminKey;

        return AdminKey.Matches(context.HttpContext.Request, expected)
            ? await next(context)
            : Results.NotFound();
    }

    private sealed class PendingRow
    {
        public int VideoId { get; init; }
        public string AuthorHandle { get; init; } = "";
        public long TweetId { get; init; }
        public byte MediaIndex { get; init; }
        public string Title { get; init; } = "";
        public string? TagsCsv { get; init; }
        public string? PosterPath { get; init; }
        public string? Mp4Path { get; init; }
        public short? Width { get; init; }
        public short? Height { get; init; }
        public int? DupOf { get; init; }
        public byte? DupDist { get; init; }
        public DateTime CreatedUtc { get; init; }
    }
}
