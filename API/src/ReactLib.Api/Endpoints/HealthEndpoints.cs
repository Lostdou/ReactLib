using System.Diagnostics;
using Microsoft.Extensions.Options;
using ReactLib.Api.Data;
using ReactLib.Api.Model;
using ReactLib.Api.Shared;

namespace ReactLib.Api.Endpoints;

public static class HealthEndpoints
{
    private static readonly long StartedAt = Stopwatch.GetTimestamp();

    public static void MapHealth(this IEndpointRouteBuilder app)
    {
        // sin DB a proposito: el front le pega antes del bootstrap solo para despertar el app pool
        app.MapGet("/api/health", () => new HealthInfo(
            "ok",
            typeof(HealthEndpoints).Assembly.GetName().Version?.ToString(3) ?? "0",
            (long)Stopwatch.GetElapsedTime(StartedAt).TotalSeconds));

        app.MapGet("/api/health/db", async (ISqlRunner sql, CancellationToken ct) =>
        {
            try
            {
                await sql.QuerySingleOrDefaultAsync<int>("SELECT 1", null, ct);
                return Results.Ok("ok");
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        app.MapGet("/api/health/mem", (HttpRequest request, IOptions<ApiOptions> options, ISqlRunner sql) =>
        {
            if (!AdminKey.Matches(request, options.Value.AdminKey)) return Results.NotFound();

            using var process = Process.GetCurrentProcess();
            return Results.Ok(new MemoryInfo(
                GC.GetTotalMemory(false),
                GC.GetGCMemoryInfo().TotalCommittedBytes,
                process.PrivateMemorySize64,
                sql.CommandCount,
                GC.CollectionCount(0),
                GC.CollectionCount(1),
                GC.CollectionCount(2)));
        });
    }
}
