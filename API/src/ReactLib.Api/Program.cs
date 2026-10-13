using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using ReactLib.Api.Data;
using ReactLib.Api.Endpoints;
using ReactLib.Api.Ingest;
using ReactLib.Api.Model;
using ReactLib.Api.Shared;

const string WebCorsPolicy = "web";

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ApiOptions>(builder.Configuration.GetSection(ApiOptions.SectionName));

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJson.Default));

// sin throw al arrancar: una config incompleta no puede tumbar /api/health, que es lo que el
// front usa para despertar el app pool. El problema se ve en /api/health/db
builder.Services.AddSingleton<ISqlRunner>(_ =>
    new SqlRunner(builder.Configuration.GetConnectionString("Db") ?? string.Empty));

builder.Services.AddSingleton(sp => new VideoQueries(
    sp.GetRequiredService<ISqlRunner>(),
    sp.GetRequiredService<IOptions<ApiOptions>>().Value.CopyFormat));

builder.Services.AddHttpClient<ITweetMetadataResolver, TweetMetadataResolver>(http =>
{
    http.Timeout = TimeSpan.FromSeconds(8);
    http.DefaultRequestHeaders.UserAgent.ParseAdd("reactlib/0.1");
});

builder.Services.AddScoped<VideoIngestor>();
builder.Services.AddScoped<SubmissionService>();
builder.Services.AddSingleton<TagCatalog>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TagCatalog>());

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(SubmissionEndpoints.RateLimitPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            // la IP sale de X-Forwarded-For via UseForwardedHeaders: detras de IIS la directa es el proxy
            context.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromHours(1) }));
});

builder.Services.AddCors(options => options.AddPolicy(WebCorsPolicy, policy =>
{
    var origins = builder.Configuration
        .GetSection($"{ApiOptions.SectionName}:AllowedOrigins").Get<string[]>() ?? [];

    policy.WithOrigins(origins)
        .AllowAnyHeader()
        .WithMethods("GET", "POST", "PATCH")
        .WithExposedHeaders("ETag")
        .SetPreflightMaxAge(TimeSpan.FromHours(24));
}));

// detras de IIS/ARR el RemoteIpAddress es el del proxy, y sobre eso se rate-limitea en la fase 3
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseCors(WebCorsPolicy);
app.UseRateLimiter();

app.MapHealth();
app.MapVideos();
app.MapSubmissions();
app.MapAdmin();

app.Run();
