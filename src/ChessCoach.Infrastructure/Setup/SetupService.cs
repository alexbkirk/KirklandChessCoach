using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Settings;
using ChessCoach.Core.Setup;
using ChessCoach.Infrastructure.Data;
using Dapper;
using Microsoft.Extensions.Options;

namespace ChessCoach.Infrastructure.Setup;

/// <summary>Works out the setup status from the database and the data folder, and saves the chosen username.</summary>
public sealed class SetupService(IAppSettingsStore settings, IGameRepository games, SqliteConnectionFactory connections, IOptions<ChessCoachOptions> options) : ISetupService
{
    public const string UsernameKey = AppSettingKeys.Username;
    public const string ImportFinishedKey = "import_finished";

    public async Task<SetupStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var username = await settings.GetAsync(UsernameKey, cancellationToken);
        var dataDirectory = options.Value.EffectiveDataDirectory;
        var missing = EngineCatalog.Missing(path => File.Exists(Path.Combine(dataDirectory, path)));
        var summary = await games.GetOverallSummaryAsync(cancellationToken);
        var finished = await settings.GetAsync(ImportFinishedKey, cancellationToken) switch
        {
            "true" => true,
            "false" => false,
            _ => (bool?)null,
        };

        await using var connection = await connections.OpenAsync(cancellationToken);
        var unanalysed = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM games WHERE id NOT IN (SELECT game_id FROM game_analysis)",
            cancellationToken: cancellationToken));

        return new SetupStatus(string.IsNullOrWhiteSpace(username) ? null : username, missing, summary.GameCount, unanalysed, finished);
    }

    public Task MarkImportStartedAsync(CancellationToken cancellationToken = default) =>
        settings.SetAsync(ImportFinishedKey, "false", cancellationToken);

    public Task MarkImportFinishedAsync(CancellationToken cancellationToken = default) =>
        settings.SetAsync(ImportFinishedKey, "true", cancellationToken);

    public async Task<string?> SaveUsernameAsync(string? username, CancellationToken cancellationToken = default)
    {
        if (!ChessComUsername.IsValid(username))
        {
            return "Enter the username exactly as it appears on chess.com: 3 to 25 letters, numbers, _ or -.";
        }

        await settings.SetAsync(UsernameKey, username!.Trim(), cancellationToken);
        return null;
    }
}
