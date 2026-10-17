using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Domain;
using ChessCoach.Core.Stats;
using Dapper;

namespace ChessCoach.Infrastructure.Data;

public sealed class OpponentRepository(SqliteConnectionFactory connections) : IOpponentRepository
{
    private const string PhaseCase =
        $"CASE WHEN (ply + 1) / 2 <= 12 THEN '{OpponentCalculator.Opening}' WHEN (ply + 1) / 2 <= 30 THEN '{OpponentCalculator.Middlegame}' ELSE '{OpponentCalculator.Late}' END";

    public async Task<(IReadOnlyList<OpponentGame> Games, IReadOnlyList<OpponentPly> Plies)> LoadAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        var games = await connection.QueryAsync<GameRow>(new CommandDefinition(
            """
            SELECT g.id AS GameId, CAST(g.date AS TEXT) AS Date, CAST(g.time AS TEXT) AS Time, CAST(g.color AS TEXT) AS Color,
                   CAST(g.result AS TEXT) AS Result, CAST(g.ending AS TEXT) AS Ending, g.myRating AS MyRating, g.oppRating AS OppRating,
                   CAST(g.family AS TEXT) AS Family, CAST(g.opening AS TEXT) AS Opening, CAST(g.url AS TEXT) AS Url,
                   CAST(g.opponent AS TEXT) AS Opponent
            FROM games g WHERE g.opponent = @name COLLATE NOCASE
            """,
            new { name },
            cancellationToken: cancellationToken));
        // CROSS JOIN: start from the opponent's few games and look moves up by primary key (see OpeningRepository).
        var plies = await connection.QueryAsync<PlyRow>(new CommandDefinition(
            """
            SELECT m.game_id AS GameId, m.ply AS Ply, m.is_mine AS IsMine, m.san AS San, m.uci AS Uci, m.fen_before AS Fen,
                   m.best_san AS BestSan, m.best_uci AS BestUci, COALESCE(m.cp_loss, 0) AS CpLoss, COALESCE(m.class, '') AS Class,
                   COALESCE(m.is_last, 0) AS IsLast, m.clk AS Clock,
                   (SELECT json_extract(n.note, '$.headline') FROM blunder_notes n WHERE n.game_id = m.game_id AND n.ply = m.ply) AS NoteHeadline
            FROM games g CROSS JOIN moves m ON m.game_id = g.id
            WHERE g.opponent = @name COLLATE NOCASE
            ORDER BY m.game_id, m.ply
            """,
            new { name },
            cancellationToken: cancellationToken));
        return (
            games.Select(g => new OpponentGame(g.GameId, g.Date, g.Time ?? "", g.Color ?? "", g.Result ?? "", g.Ending ?? "", g.MyRating, g.OppRating,
                g.Family ?? "", g.Opening ?? "", g.Url ?? "", g.Opponent ?? "")).ToList(),
            plies.Select(p => new OpponentPly(p.GameId, p.Ply, p.IsMine != 0, p.San, p.Uci, p.Fen, p.BestSan, p.BestUci, p.CpLoss, p.Class,
                p.IsLast != 0, p.Clock, p.NoteHeadline)).ToList());
    }

    public async Task<PhaseBaseline> LoadBaselineAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        var rows = (await connection.QueryAsync<BaselineRow>(new CommandDefinition(
            $"""
            SELECT is_mine AS IsMine, {PhaseCase} AS Phase,
                   COUNT(*) AS Moves, AVG(COALESCE(cp_loss, 0)) AS AvgCpLoss,
                   100.0 * SUM(CASE WHEN class = 'blunder' THEN 1 ELSE 0 END) / COUNT(*) AS BlunderPercent
            FROM moves
            WHERE COALESCE(is_last, 0) = 0
            GROUP BY is_mine, Phase
            """,
            cancellationToken: cancellationToken))).ToList();
        return new PhaseBaseline(
            rows.Where(r => r.IsMine != 0).ToDictionary(r => r.Phase, r => new PhaseRate(r.Moves, r.AvgCpLoss, r.BlunderPercent)),
            rows.Where(r => r.IsMine == 0).ToDictionary(r => r.Phase, r => new PhaseRate(r.Moves, r.AvgCpLoss, r.BlunderPercent)));
    }

    private sealed class GameRow
    {
        public string GameId { get; set; } = "";

        public string Date { get; set; } = "";

        public string? Time { get; set; }

        public string? Color { get; set; }

        public string? Result { get; set; }

        public string? Ending { get; set; }

        public int MyRating { get; set; }

        public int OppRating { get; set; }

        public string? Family { get; set; }

        public string? Opening { get; set; }

        public string? Url { get; set; }

        public string? Opponent { get; set; }
    }

    private sealed class PlyRow
    {
        public string GameId { get; set; } = "";

        public int Ply { get; set; }

        public int IsMine { get; set; }

        public string San { get; set; } = "";

        public string Uci { get; set; } = "";

        public string Fen { get; set; } = "";

        public string? BestSan { get; set; }

        public string? BestUci { get; set; }

        public int CpLoss { get; set; }

        public string Class { get; set; } = "";

        public int IsLast { get; set; }

        public double? Clock { get; set; }

        public string? NoteHeadline { get; set; }
    }

    private sealed class BaselineRow
    {
        public int IsMine { get; set; }

        public string Phase { get; set; } = "";

        public int Moves { get; set; }

        public double AvgCpLoss { get; set; }

        public double BlunderPercent { get; set; }
    }
}
