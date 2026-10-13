using System.Text;
using Dapper;
using ReactLib.Api.Shared;

namespace ReactLib.Api.Data;

public sealed class SearchTerms
{
    private const int MaxTerms = 3;
    private const int MaxTermLength = 30;

    public static readonly SearchTerms None = new([]);

    private readonly string[] _terms;

    private SearchTerms(string[] terms) => _terms = terms;

    public bool IsEmpty => _terms.Length == 0;

    public static SearchTerms From(string? query)
    {
        var folded = TextNorm.Fold(query, MaxTerms * (MaxTermLength + 1));
        if (folded.Length == 0) return None;

        var terms = folded.Split(' ', MaxTerms, StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < terms.Length; i++)
            if (terms[i].Length > MaxTermLength) terms[i] = terms[i][..MaxTermLength];

        return terms.Length == 0 ? None : new SearchTerms(terms);
    }

    public string Clause(string column)
    {
        if (IsEmpty) return string.Empty;

        var clause = new StringBuilder();
        for (var i = 0; i < _terms.Length; i++)
            clause.Append($" AND {column} LIKE @term{i}");

        return clause.ToString();
    }

    public DynamicParameters Bind(DynamicParameters parameters)
    {
        for (var i = 0; i < _terms.Length; i++)
        {
            // IsAnsi obligatorio: Dapper manda nvarchar por defecto y la conversion implicita
            // contra la columna varchar degrada IX_Video_Search a scan del clustered
            parameters.Add($"term{i}", new DbString
            {
                Value = $"%{_terms[i]}%",
                IsAnsi = true,
                IsFixedLength = false,
                Length = MaxTermLength + 2
            });
        }

        return parameters;
    }
}
