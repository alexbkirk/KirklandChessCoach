using ChessCoach.Core.Abstractions;
using Dapper;

namespace ChessCoach.Infrastructure.Data;

public sealed class AppSettingsStore(SqliteConnectionFactory connections) : IAppSettingsStore
{
    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT value FROM app_settings WHERE key = @key",
            new { key },
            cancellationToken: cancellationToken));
    }

    public async Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO app_settings(key, value) VALUES (@key, @value) ON CONFLICT(key) DO UPDATE SET value = excluded.value",
            new { key, value },
            cancellationToken: cancellationToken));
    }
}
