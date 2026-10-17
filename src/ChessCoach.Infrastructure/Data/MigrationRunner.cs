using System.Globalization;
using System.Reflection;
using Dapper;
using Microsoft.Extensions.Logging;

namespace ChessCoach.Infrastructure.Data;

/// <summary>
/// Applies the embedded NNNN_name.sql migrations in order and records them in schema_version.
/// The app owns the schema; the Python scripts only read and write rows.
/// </summary>
public sealed partial class MigrationRunner(SqliteConnectionFactory connections, ILogger<MigrationRunner> logger)
{
    private static readonly Assembly MigrationAssembly = typeof(MigrationRunner).Assembly;

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);

        // WAL lets the UI read while a Python refresh writes. It is persistent, and cannot be set inside a transaction.
        var mode = await connection.ExecuteScalarAsync<string>("PRAGMA journal_mode = WAL;");
        LogJournalMode(mode);

        await connection.ExecuteAsync(
            "CREATE TABLE IF NOT EXISTS schema_version(version INTEGER PRIMARY KEY, name TEXT NOT NULL, applied_utc TEXT NOT NULL);");
        var applied = (await connection.QueryAsync<int>("SELECT version FROM schema_version")).ToHashSet();

        foreach (var migration in LoadMigrations())
        {
            if (applied.Contains(migration.Version))
            {
                continue;
            }

            LogApplying(migration.Version, migration.Name);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await connection.ExecuteAsync(migration.Sql, transaction: transaction);
            await connection.ExecuteAsync(
                "INSERT INTO schema_version(version, name, applied_utc) VALUES (@Version, @Name, @AppliedUtc)",
                new { migration.Version, migration.Name, AppliedUtc = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture) },
                transaction);
            await transaction.CommitAsync(cancellationToken);
        }
    }

    internal static IReadOnlyList<Migration> LoadMigrations()
    {
        var migrations = new List<Migration>();
        foreach (var resource in MigrationAssembly.GetManifestResourceNames())
        {
            const string marker = ".Migrations.";
            var index = resource.IndexOf(marker, StringComparison.Ordinal);
            if (index < 0 || !resource.EndsWith(".sql", StringComparison.Ordinal))
            {
                continue;
            }

            var fileName = resource[(index + marker.Length)..^".sql".Length];   // 0001_baseline
            var separator = fileName.IndexOf('_', StringComparison.Ordinal);
            if (separator <= 0 || !int.TryParse(fileName[..separator], NumberStyles.None, CultureInfo.InvariantCulture, out var version))
            {
                throw new InvalidOperationException($"Migration '{resource}' must be named NNNN_name.sql.");
            }

            using var stream = MigrationAssembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            migrations.Add(new Migration(version, fileName[(separator + 1)..], reader.ReadToEnd()));
        }

        migrations.Sort((a, b) => a.Version.CompareTo(b.Version));
        for (var i = 1; i < migrations.Count; i++)
        {
            if (migrations[i].Version == migrations[i - 1].Version)
            {
                throw new InvalidOperationException($"Duplicate migration version {migrations[i].Version}.");
            }
        }

        return migrations;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SQLite journal mode: {Mode}")]
    private partial void LogJournalMode(string? mode);

    [LoggerMessage(Level = LogLevel.Information, Message = "Applying migration {Version} {Name}")]
    private partial void LogApplying(int version, string name);

    internal sealed record Migration(int Version, string Name, string Sql);
}

