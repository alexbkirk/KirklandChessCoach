using System.Globalization;
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

    public async Task<(IReadOnlyList<VariationGame> Games, IReadOnlyList<VariationPly> Plies)> LoadVariationAsync(OpeningScope scope, CancellationToken cancellationToken = default)
    {
        const string Where = "g.color = @Color AND (@Opening IS NULL OR g.opening = @Opening) AND (@Family IS NULL OR g.family = @Family)";
        var args = new { scope.Color, scope.Opening, scope.Family };
        await using var connection = await connections.OpenAsync(cancellationToken);
        var games = await connection.QueryAsync<GameRow>(new CommandDefinition(
            $"""
            SELECT g.id AS GameId, COALESCE(g.result, '') AS Result, CAST(g.date AS TEXT) AS Date,
                   COALESCE(g.opponent, '') AS Opponent, COALESCE(g.url, '') AS Url
            FROM games g WHERE {Where}
            """,
            args,
            cancellationToken: cancellationToken));
        // CROSS JOIN makes SQLite start from the (few) games and look moves up by primary key, instead of scanning moves.
        var plies = await connection.QueryAsync<PlyRow>(new CommandDefinition(
            $"""
            SELECT m.game_id AS GameId, m.ply AS Ply, m.fen_before AS Fen, m.san AS San, m.uci AS Uci, m.is_mine AS IsMine,
                   m.best_san AS BestSan, m.best_uci AS BestUci, COALESCE(m.cp_loss, 0) AS CpLoss
            FROM games g CROSS JOIN moves m ON m.game_id = g.id AND m.ply <= 30
            WHERE {Where}
            ORDER BY m.game_id, m.ply
            """,
            args,
            cancellationToken: cancellationToken));
        return (
            games.Select(g => new VariationGame(g.GameId, g.Result, g.Date, g.Opponent, g.Url)).ToList(),
            plies.Select(p => new VariationPly(p.GameId, p.Ply, p.Fen, p.San, p.Uci, p.IsMine != 0, p.BestSan, p.BestUci, p.CpLoss)).ToList());
    }

    public async Task<IReadOnlyDictionary<string, PracticeTally>> LoadPracticeAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default)
    {
        if (keys.Count == 0) return new Dictionary<string, PracticeTally>();
        await using var connection = await connections.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<PracticeRow>(new CommandDefinition(
            """
            SELECT p.position_key AS PositionKey, COUNT(*) AS Tries, SUM(p.correct) AS Correct,
                   (SELECT l.correct FROM opening_practice l WHERE l.position_key = p.position_key ORDER BY l.id DESC LIMIT 1) AS LastCorrect
            FROM opening_practice p
            WHERE p.position_key IN @keys
            GROUP BY p.position_key
            """,
            new { keys },
            cancellationToken: cancellationToken));
        return rows.ToDictionary(r => r.PositionKey, r => new PracticeTally(r.Tries, r.Correct, r.LastCorrect != 0));
    }

    public async Task RecordPracticeAsync(string key, string playedUci, bool correct, DateTimeOffset atUtc, CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO opening_practice(position_key, played_uci, correct, at_utc) VALUES (@key, @playedUci, @correct, @at)",
            new { key, playedUci, correct = correct ? 1 : 0, at = atUtc.ToString("o", CultureInfo.InvariantCulture) },
            cancellationToken: cancellationToken));
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

    private sealed class GameRow
    {
        public string GameId { get; set; } = "";

        public string Result { get; set; } = "";

        public string Date { get; set; } = "";

        public string Opponent { get; set; } = "";

        public string Url { get; set; } = "";
    }

    private sealed class PlyRow
    {
        public string GameId { get; set; } = "";

        public int Ply { get; set; }

        public string Fen { get; set; } = "";

        public string San { get; set; } = "";

        public string Uci { get; set; } = "";

        public int IsMine { get; set; }

        public string? BestSan { get; set; }

        public string? BestUci { get; set; }

        public int CpLoss { get; set; }
    }

    private sealed class PracticeRow
    {
        public string PositionKey { get; set; } = "";

        public int Tries { get; set; }

        public int Correct { get; set; }

        public int LastCorrect { get; set; }
    }
}
