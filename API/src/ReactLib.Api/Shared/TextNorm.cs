using System.Text;

namespace ReactLib.Api.Shared;

public static class TextNorm
{
    private const char Discard = '\0';

    public static string Fold(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var folded = new StringBuilder(Math.Min(value.Length, maxLength));
        var pendingSeparator = false;

        foreach (var original in value)
        {
            if (folded.Length >= maxLength) break;

            var ch = FoldChar(original);
            if (ch == Discard) continue;

            if (ch == ' ')
            {
                pendingSeparator = folded.Length > 0;
                continue;
            }

            if (pendingSeparator)
            {
                folded.Append(' ');
                pendingSeparator = false;
            }

            folded.Append(ch);
        }

        return folded.ToString();
    }

    public static string Slug(string? value, int maxLength = 30)
        => Fold(value, maxLength).Replace(' ', '-');

    // tabla a mano y no string.Normalize: tiene que dar exactamente igual que dbo.FoldText en SQL
    private static char FoldChar(char ch) => ch switch
    {
        >= 'a' and <= 'z' => ch,
        >= 'A' and <= 'Z' => char.ToLowerInvariant(ch),
        >= '0' and <= '9' => ch,
        'á' or 'à' or 'ä' or 'â' or 'Á' or 'À' or 'Ä' or 'Â' => 'a',
        'é' or 'è' or 'ë' or 'ê' or 'É' or 'È' or 'Ë' or 'Ê' => 'e',
        'í' or 'ì' or 'ï' or 'î' or 'Í' or 'Ì' or 'Ï' or 'Î' => 'i',
        'ó' or 'ò' or 'ö' or 'ô' or 'Ó' or 'Ò' or 'Ö' or 'Ô' => 'o',
        'ú' or 'ù' or 'ü' or 'û' or 'Ú' or 'Ù' or 'Ü' or 'Û' => 'u',
        'ñ' or 'Ñ' => 'n',
        'ç' or 'Ç' => 'c',
        ' ' or '\t' or '\n' or '\r' or '-' or '_' or '.' or ',' or '/' => ' ',
        _ => Discard
    };
}
