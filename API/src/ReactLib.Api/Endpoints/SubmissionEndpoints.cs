using ReactLib.Api.Ingest;
using ReactLib.Api.Model;

namespace ReactLib.Api.Endpoints;

public sealed record ResolveRequest(string Url);

public sealed record SubmitRequest(string Url, string? Tags, long? Dhash);

public static class SubmissionEndpoints
{
    public const string RateLimitPolicy = "submissions";

    public static void MapSubmissions(this IEndpointRouteBuilder app)
    {
        var submissions = app.MapGroup("/api/submissions").RequireRateLimiting(RateLimitPolicy);

        submissions.MapPost("/resolve", Resolve);
        submissions.MapPost("", Submit);

        app.MapGet("/api/tags", (string? q, TagCatalog tags) => Results.Ok(tags.Search(q).ToArray()));
    }

    private static async Task<IResult> Resolve(
        ResolveRequest request,
        SubmissionService submissions,
        CancellationToken ct)
    {
        var result = await submissions.ResolveAsync(request.Url, ct);
        return Respond(result);
    }

    private static async Task<IResult> Submit(
        SubmitRequest request,
        SubmissionService submissions,
        HttpContext context,
        CancellationToken ct)
    {
        var result = await submissions.SubmitAsync(
            request.Url, request.Tags, request.Dhash, context.Connection.RemoteIpAddress?.ToString(), ct);

        return Respond(result);
    }

    private static IResult Respond(SubmitResult result)
    {
        var body = new SubmitResponse(
            result.Outcome.ToString(), result.Preview, result.Duplicate, result.Seq, result.ProposedTags);

        return result.Outcome switch
        {
            SubmitOutcome.Published => Results.Ok(body),
            SubmitOutcome.AlreadyExists => Results.Conflict(body),
            _ => Results.BadRequest(body),
        };
    }
}
