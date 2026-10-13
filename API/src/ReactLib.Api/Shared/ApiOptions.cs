using System.Security.Cryptography;
using System.Text;
using ReactLib.Api.Data;

namespace ReactLib.Api.Shared;

public sealed class ApiOptions
{
    public const string SectionName = "Api";

    public string CopyFormat { get; init; } = CopyFormats.Media;
    public string[] AllowedOrigins { get; init; } = [];
    public string AdminKey { get; init; } = "";
    public string IpHashSalt { get; init; } = "";
}

public static class AdminKey
{
    public const string HeaderName = "X-Admin-Key";

    public static bool Matches(HttpRequest request, string expected)
    {
        if (string.IsNullOrEmpty(expected)) return false;

        var provided = request.Headers[HeaderName].ToString();
        if (provided.Length == 0) return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided),
            Encoding.UTF8.GetBytes(expected));
    }
}
