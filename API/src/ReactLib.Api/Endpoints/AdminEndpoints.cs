using Microsoft.Extensions.Options;
using ReactLib.Api.Ingest;
using ReactLib.Api.Model;
using ReactLib.Api.Shared;

namespace ReactLib.Api.Endpoints;

public sealed record IngestRequest(string Url, string? Title, string? Tags, long? Dhash);

public static class AdminEndpoints
{
    public static void MapAdmin(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin").AddEndpointFilter(RequireAdminKey);

        admin.MapPost("/videos", Ingest);
    }

    private static async Task<IResult> Ingest(
        IngestRequest request,
        VideoIngestor ingestor,
        CancellationToken ct)
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
}
