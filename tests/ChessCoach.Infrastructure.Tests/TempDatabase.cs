using ChessCoach.Infrastructure;
using ChessCoach.Infrastructure.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ChessCoach.Infrastructure.Tests;

/// <summary>A throw-away data folder with its own SQLite file, deleted on dispose.</summary>
internal sealed class TempDatabase : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "chesscoach-tests-" + Guid.NewGuid().ToString("N"));

    public TempDatabase()
    {
        Directory.CreateDirectory(_directory);
        Options = new ChessCoachOptions { DataDirectory = _directory };
        Connections = new SqliteConnectionFactory(Microsoft.Extensions.Options.Options.Create(Options));
        Migrations = new MigrationRunner(Connections, NullLogger<MigrationRunner>.Instance);
    }

    public ChessCoachOptions Options { get; }

    public SqliteConnectionFactory Connections { get; }

    public MigrationRunner Migrations { get; }

    /// <summary>Runs raw SQL, for arranging pre-existing data (for example the Python-era schema).</summary>
    public async Task ExecuteAsync(string sql)
    {
        await using var connection = await Connections.OpenAsync();
        await connection.ExecuteAsync(sql);
    }

    public async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = await Connections.OpenAsync();
        return (await connection.ExecuteScalarAsync<T>(sql))!;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // best effort: a lingering handle must not fail the test run
        }
    }
}
