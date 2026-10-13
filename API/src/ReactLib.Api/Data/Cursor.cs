using System.Text;

namespace ReactLib.Api.Data;

public static class Cursor
{
    private const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";
    private const char PartSeparator = '.';

    public static readonly int FirstSeq = int.MaxValue;
    public static readonly int FirstScore = int.MaxValue;

    public static string Encode(int seq) => ToBase36(seq);

    public static string Encode(int score, int seq) => $"{ToBase36(score)}{PartSeparator}{ToBase36(seq)}";

    public static int DecodeSeq(string? cursor) => TryBase36(cursor, out var seq) ? seq : FirstSeq;

    public static (int Score, int Seq) DecodeScore(string? cursor)
    {
        if (string.IsNullOrEmpty(cursor)) return (FirstScore, FirstSeq);

        var parts = cursor.Split(PartSeparator);
        if (parts.Length != 2 || !TryBase36(parts[0], out var score) || !TryBase36(parts[1], out var seq))
            return (FirstScore, FirstSeq);

        return (score, seq);
    }

    private static string ToBase36(int value)
    {
        if (value <= 0) return "0";

        var digits = new StringBuilder(7);
        for (var remaining = value; remaining > 0; remaining /= Alphabet.Length)
            digits.Insert(0, Alphabet[remaining % Alphabet.Length]);

        return digits.ToString();
    }

    private static bool TryBase36(string? text, out int value)
    {
        value = 0;
        if (string.IsNullOrEmpty(text) || text.Length > 7) return false;

        foreach (var ch in text)
        {
            var digit = Alphabet.IndexOf(ch);
            if (digit < 0) return false;

            value = value * Alphabet.Length + digit;
        }

        return true;
    }
}
