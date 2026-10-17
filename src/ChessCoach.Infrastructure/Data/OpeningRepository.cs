using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Domain;
using Dapper;

namespace ChessCoach.Infrastructure.Data;

public sealed class OpeningRepository(SqliteConnectionFactory connections) : IOpeningRepository
{
    /// <summary>
    /// Evaluations are stored from White's side; the report uses your side, clamped to +/-1000 centipawns.
    /// Your 10th move is ply 19 as White and ply 20 as Black. Looking it up by (game_id, ply) keeps the query fast:
    /// filtering by ply number alone makes SQLite scan every move, which froze the UI for about a minute.
    /// </summary>
    private const int EvalCap = 1000;

    public async Task<IReadOnlyList<OpeningGame>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<Row>(new CommandDefinition(
            """
            WITH mine AS (
                SELECT game_id, ply, class, is_last, cp_loss FROM moves WHERE is_mine = 1
            ),
            per_game AS (
                SELECT game_id,
                       SUM(CASE WHEN is_last = 0 AND class = 'blunder' THEN 1 ELSE 0 END) AS Blunders,
                       MIN(CASE WHEN is_last = 0 AND ply <= 30 AND cp_loss >= 100 THEN (ply + 1) / 2 END) AS FirstSlipMove
                FROM mine
                GROUP BY game_id
            )
            SELECT g.id AS Id, g.color AS Color, COALESCE(g.family, '') AS Family, COALESCE(g.opening, '') AS Opening,
                   g.result AS Result,
                   COALESCE(p.Blunders, 0) AS Blunders,
                   p.FirstSlipMove AS FirstSlipMove,
                   (SELECT m.eval_after FROM moves m
                     WHERE m.game_id = g.id AND m.ply = CASE WHEN g.color = 'white' THEN 19 ELSE 20 END AND m.is_mine = 1) AS EvalAt10White
            FROM games g
            LEFT JOIN per_game p ON p.game_id = g.id
            """,
            cancellationToken: cancellationToken));

        return rows.Select(r => new OpeningGame(
            Id: r.Id,
            Color: r.Color,
            Family: r.Family,
            Opening: r.Opening,
            Won: r.Result == "win",
            Blunders: r.Blunders,
            FirstSlipMove: r.FirstSlipMove,
            EvalAt10: r.EvalAt10White is null ? null : Clamp(r.Color == "white" ? r.EvalAt10White.Value : -r.EvalAt10White.Value))).ToList();
    }

    private static int Clamp(int cp) => Math.Clamp(cp, -EvalCap, EvalCap);

    private sealed class Row
    {
        public string Id { get; set; } = "";

        public string Color { get; set; } = "";

        public string Family { get; set; } = "";

        public string Opening { get; set; } = "";

        public string? Result { get; set; }

        public int Blunders { get; set; }

        public int? FirstSlipMove { get; set; }

        public int? EvalAt10White { get; set; }
    }
}
