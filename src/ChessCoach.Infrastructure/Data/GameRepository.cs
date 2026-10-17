using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Domain;
using Dapper;

namespace ChessCoach.Infrastructure.Data;

public sealed class GameRepository(SqliteConnectionFactory connections) : IGameRepository
{
    public async Task<DatabaseSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);

        // The Python-created games table has untyped columns, which SQLite reports as BLOB when a result is NULL or computed.
        // Cast text columns explicitly, and map through a settable DTO so Dapper converts SQLite's Int64.
        var row = await connection.QuerySingleAsync<SummaryRow>(new CommandDefinition(
            """
            SELECT (SELECT COUNT(*) FROM games) AS GameCount,
                   (SELECT COUNT(*) FROM moves) AS MoveCount,
                   (SELECT CAST(MIN(date) AS TEXT) FROM games) AS FirstGameDate,
                   (SELECT CAST(MAX(date) AS TEXT) FROM games) AS LastGameDate
            """,
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
