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

builder.Services.AddSingleton<ISqlRunner>(_ =>
    new SqlRunner(builder.Configuration.GetConnectionString("Db")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Db")));

builder.Services.AddSingleton(sp => new VideoQueries(
    sp.GetRequiredService<ISqlRunner>(),
    sp.GetRequiredService<IOptions<ApiOptions>>().Value.CopyFormat));

builder.Services.AddHttpClient<ITweetMetadataResolver, TweetMetadataResolver>(http =>
{
    http.Timeout = TimeSpan.FromSeconds(8);
    http.DefaultRequestHeaders.UserAgent.ParseAdd("reactlib/0.1");
});

builder.Services.AddScoped<VideoIngestor>();

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

app.MapHealth();
app.MapVideos();
app.MapAdmin();

app.Run();
