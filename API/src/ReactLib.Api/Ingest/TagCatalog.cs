using ReactLib.Api.Data;
using ReactLib.Api.Shared;

namespace ReactLib.Api.Ingest;

public sealed record TagSuggestion(string Name, int Uses);

/// Mantiene los tags existentes en memoria: un autocomplete que pega a la DB en cada tecla es
/// insostenible con 8 conexiones y cold starts.
public sealed class TagCatalog(ISqlRunner sql, ILogger<TagCatalog> log) : BackgroundService
{
    private const int MaxSuggestions = 12;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(10);

    private TagSuggestion[] _tags = [];

    public IReadOnlyList<TagSuggestion> Search(string? query)
    {
        var all = _tags;
        var prefix = TextNorm.Slug(query);

        if (prefix.Length == 0) return all.Take(MaxSuggestions).ToArray();

        var matches = all.Where(t => t.Name.StartsWith(prefix, StringComparison.Ordinal)).ToList();
        if (matches.Count < MaxSuggestions)
            matches.AddRange(all.Where(t => !t.Name.StartsWith(prefix, StringComparison.Ordinal)
                                            && t.Name.Contains(prefix, StringComparison.Ordinal)));

        return matches.Take(MaxSuggestions).ToArray();
    }

    public bool Exists(string slug) => Array.Exists(_tags, t => t.Name == slug);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(RefreshInterval);
        do
        {
            await RefreshAsync(ct);
        }
        while (await timer.WaitForNextTickAsync(ct));
    }

    public async Task RefreshAsync(CancellationToken ct)
    {
        try
        {
            var rows = await sql.QueryAsync<TagSuggestion>(
                "SELECT TOP (2000) NameNorm AS Name, UseCount AS Uses FROM dbo.Tag ORDER BY UseCount DESC, NameNorm",
                null, ct);

            _tags = [.. rows];
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            log.LogWarning(ex, "no se pudo refrescar el catálogo de tags");
        }
    }
}
