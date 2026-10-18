using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Domain;
using ChessCoach.Core.Settings;
using Dapper;

namespace ChessCoach.Infrastructure.Data;

public sealed class GameRepository(SqliteConnectionFactory connections, TimeClassView? timeClass = null) : IGameRepository
{
    public async Task<DatabaseSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);

        // The Python-created games table has untyped columns, which SQLite reports as BLOB when a result is NULL or computed.
        // Cast text columns explicitly, and map through a settable DTO so Dapper converts SQLite's Int64.
        var row = await connection.QuerySingleAsync<SummaryRow>(new CommandDefinition(
            """
            SELECT (SELECT COUNT(*) FROM games WHERE (@tc IS NULL OR timeClass = @tc)) AS GameCount,
                   (SELECT COUNT(*) FROM moves WHERE (@tc IS NULL OR game_id IN (SELECT id FROM games WHERE timeClass = @tc))) AS MoveCount,
                   (SELECT CAST(MIN(date) AS TEXT) FROM games WHERE (@tc IS NULL OR timeClass = @tc)) AS FirstGameDate,
                   (SELECT CAST(MAX(date) AS TEXT) FROM games WHERE (@tc IS NULL OR timeClass = @tc)) AS LastGameDate
            """,
            new { tc = timeClass?.Current },
            cancellationToken: cancellationToken));
        return new DatabaseSummary(row.GameCount, row.MoveCount, row.FirstGameDate, row.LastGameDate);
    }

    private sealed class SummaryRow
    {
        public int GameCount { get; set; }

        public int MoveCount { get; set; }

        public string? FirstGameDate { get; set; }

        public string? LastGameDate { get; set; }
    }
}
