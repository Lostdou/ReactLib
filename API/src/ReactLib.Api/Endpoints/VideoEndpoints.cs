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

    private static GridSort ParseSort(string? sort) =>
        string.Equals(sort, "top", StringComparison.OrdinalIgnoreCase) ? GridSort.Top : GridSort.New;
}
