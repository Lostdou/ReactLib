using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ReactLib.Api.Ingest;

public interface IThumbnailHasher
{
    Task<long?> HashAsync(string posterPath, CancellationToken ct);
}

public sealed class ThumbnailHasher(HttpClient http, ILogger<ThumbnailHasher> log) : IThumbnailHasher
{
    private const int Columns = 9;
    private const int Rows = 8;
    private const int MinBits = 8;
    private const int MaxBits = 56;
    private const int MaxDownloadBytes = 1024 * 1024;

    // X ignora ?name=thumb en los thumbs de video, asi que esto decodifica la imagen entera:
    // de a uno por vez es lo unico que mantiene el LOH tranquilo en una caja de 256 MB
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<long?> HashAsync(string posterPath, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            using var response = await http.GetAsync(TweetMetadataResolver.PosterHost + posterPath, ct);
            if (!response.IsSuccessStatusCode) return null;
            if (response.Content.Headers.ContentLength > MaxDownloadBytes) return null;

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var image = await Image.LoadAsync<L8>(stream, ct);

            image.Mutate(x => x.Resize(Columns, Rows, KnownResamplers.Triangle));

            var hash = Difference(image);
            return IsDistinctive(hash) ? hash : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "no se pudo hashear {Poster}", posterPath);
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static long Difference(Image<L8> image)
    {
        long hash = 0;
        var bit = 0;

        for (var y = 0; y < Rows; y++)
        {
            for (var x = 0; x < Columns - 1; x++, bit++)
            {
                if (image[x, y].PackedValue > image[x + 1, y].PackedValue) hash |= 1L << bit;
            }
        }

        return hash;
    }

    // un frame plano (fade a negro, pantalla blanca) matchearia contra todo el catalogo
    private static bool IsDistinctive(long hash)
    {
        var bits = BitOperations.PopCount((ulong)hash);
        return bits is >= MinBits and <= MaxBits;
    }
}
