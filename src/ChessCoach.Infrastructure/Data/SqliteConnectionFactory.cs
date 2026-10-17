using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace ChessCoach.Infrastructure.Data;

/// <summary>Opens connections to the games database with a busy timeout, because Python writes to the same file.</summary>
public sealed class SqliteConnectionFactory(IOptions<ChessCoachOptions> options)
{
    private const int BusyTimeoutMs = 5000;

    public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection($"Data Source={options.Value.DatabasePath};Mode=ReadWriteCreate");
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync($"PRAGMA busy_timeout = {BusyTimeoutMs};");
        return connection;
    }
}
