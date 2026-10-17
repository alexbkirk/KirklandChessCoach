using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Domain;
using Dapper;

namespace ChessCoach.Infrastructure.Data;

public sealed class OpeningRepository(SqliteConnectionFactory connections) : IOpeningRepository
{
    /// <summary>Evaluations are stored from White's side; the report uses your side, clamped to +/-1000 centipawns.</summary>
    private const int EvalCap = 1000;

    public async Task<IReadOnlyList<OpeningGame>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<Row>(new CommandDefinition(
            """
            SELECT g.id AS Id, g.color AS Color, COALESCE(g.family, '') AS Family, COALESCE(g.opening, '') AS Opening,
                   g.result AS Result,
                   (SELECT COUNT(*) FROM moves m
                     WHERE m.game_id = g.id AND m.is_mine = 1 AND m.is_last = 0 AND m.class = 'blunder') AS Blunders,
                   (SELECT MIN((m.ply + 1) / 2) FROM moves m
                     WHERE m.game_id = g.id AND m.is_mine = 1 AND m.is_last = 0 AND m.ply <= 30 AND m.cp_loss >= 100) AS FirstSlipMove,
                   (SELECT m.eval_after FROM moves m
                     WHERE m.game_id = g.id AND m.is_mine = 1 AND (m.ply + 1) / 2 = 10 LIMIT 1) AS EvalAt10White
            FROM games g
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
