using System.Data;
using Microsoft.Extensions.Options;
using ReactLib.Api.Data;
using ReactLib.Api.Model;
using ReactLib.Api.Shared;

namespace ReactLib.Api.Endpoints;

public static class VideoEndpoints
{
    private const int DefaultPageSize = 24;

    public static void MapVideos(this IEndpointRouteBuilder app)
    {
        var videos = app.MapGroup("/api/videos");

        videos.MapGet("", GetGrid);
        videos.MapGet("/{seq:int}", GetBySeq);
        videos.MapPost("/{seq:int}/report", Report)
            .RequireRateLimiting(SubmissionEndpoints.RateLimitPolicy);

        app.MapGet("/api/config", GetConfig);
    }

    private static async Task<IResult> GetGrid(
        string? sort,
        string? tag,
        string? q,
        string? after,
        int? size,
        VideoQueries videos,
        CancellationToken ct)
    {
        var request = new GridRequest(ParseSort(sort), tag, q, after, size ?? DefaultPageSize);
        return Results.Ok(await videos.GetGridAsync(request, ct));
    }

    private static async Task<IResult> GetBySeq(int seq, VideoQueries videos, CancellationToken ct) =>
        await videos.GetBySeqAsync(seq, ct) is { } card
            ? Results.Ok(card)
            : Results.NotFound();

    private static async Task<IResult> GetConfig(
        IOptions<ApiOptions> options,
        ISqlRunner sql,
        CancellationToken ct)
    {
        var version = await sql.QuerySingleOrDefaultAsync<int>(
            "SELECT CAST(V AS int) FROM dbo.Meta WHERE K = 'CatalogVersion'", null, ct);

        return Results.Ok(new ApiConfig(options.Value.CopyFormat, version));
    }

    // a los 3 reportes el video vuelve a pendiente y desaparece de la grilla sin que nadie intervenga
    private static async Task<IResult> Report(int seq, ISqlRunner sql, CancellationToken ct)
    {
        await sql.ExecuteAsync("dbo.ReportVideo", new { PubSeq = seq }, ct, CommandType.StoredProcedure);
        return Results.NoContent();
    }

    private static GridSort ParseSort(string? sort) =>
        string.Equals(sort, "top", StringComparison.OrdinalIgnoreCase) ? GridSort.Top : GridSort.New;
}
