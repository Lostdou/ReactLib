using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace ReactLib.Api.Data;

public interface ISqlRunner
{
    long CommandCount { get; }

    Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? param, CancellationToken ct);
    Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? param, CancellationToken ct, CommandType type = CommandType.Text);
    Task<int> ExecuteAsync(string sql, object? param, CancellationToken ct, CommandType type = CommandType.Text);
}

public sealed class SqlRunner(string connectionString) : ISqlRunner
{
    private long _commandCount;

    public long CommandCount => Interlocked.Read(ref _commandCount);

    public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? param, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        var rows = await connection.QueryAsync<T>(Track(sql, param, ct));
        return rows as IReadOnlyList<T> ?? [.. rows];
    }

    public async Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? param, CancellationToken ct, CommandType type = CommandType.Text)
    {
        await using var connection = new SqlConnection(connectionString);
        return await connection.QuerySingleOrDefaultAsync<T>(Track(sql, param, ct, type));
    }

    public async Task<int> ExecuteAsync(string sql, object? param, CancellationToken ct, CommandType type = CommandType.Text)
    {
        await using var connection = new SqlConnection(connectionString);
        return await connection.ExecuteAsync(Track(sql, param, ct, type));
    }

    private CommandDefinition Track(string sql, object? param, CancellationToken ct, CommandType type = CommandType.Text)
    {
        Interlocked.Increment(ref _commandCount);
        return new CommandDefinition(sql, param, commandType: type, cancellationToken: ct);
    }
}
