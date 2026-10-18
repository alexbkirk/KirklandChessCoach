using System.Globalization;
using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Domain;
using ChessCoach.Core.Settings;
using Dapper;

namespace ChessCoach.Infrastructure.Data;

public sealed class FocusRepository(SqliteConnectionFactory connections, TimeClassView? timeClass = null) : IFocusRepository
{
    private const string DateFormat = "yyyy-MM-dd";

    public async Task<(IReadOnlyList<FocusGame> Games, IReadOnlyList<FocusBlunder> Blunders)> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        var gameRows = await connection.QueryAsync<GameRow>(new CommandDefinition(
            "SELECT id AS Id, CAST(date AS TEXT) AS Date, CAST(opponent AS TEXT) AS Opponent, CAST(url AS TEXT) AS Url FROM games WHERE (@tc IS NULL OR timeClass = @tc) ORDER BY CAST(date AS TEXT), time",
            new { tc = timeClass?.Current },
            cancellationToken: cancellationToken));
        // The note (blunder_notes, written by analysis/explain_blunders.py) says what the move allowed or missed.
        var blunderRows = await connection.QueryAsync<BlunderRow>(new CommandDefinition(
            """
            SELECT m.game_id AS GameId, m.ply AS Ply, m.san AS San, m.fen_before AS Fen, m.color AS Color,
                   m.best_uci AS BestUci, m.best_san AS BestSan,
                   json_extract(n.note, '$.side') AS Side,
                   json_extract(n.note, '$.motif.k') AS Motif,
                   json_extract(n.note, '$.headline') AS Headline,
                   json_extract(n.note, '$.swing') AS Swing
            FROM moves m
            LEFT JOIN blunder_notes n ON n.game_id = m.game_id AND n.ply = m.ply
            WHERE m.is_mine = 1 AND m.is_last = 0 AND m.class = 'blunder'
              AND (@tc IS NULL OR m.game_id IN (SELECT id FROM games WHERE timeClass = @tc))
            """,
            new { tc = timeClass?.Current },
            cancellationToken: cancellationToken));

        var games = gameRows
            .Select(r => new FocusGame(r.Id, DateOnly.ParseExact(r.Date, DateFormat, CultureInfo.InvariantCulture), r.Opponent ?? "", r.Url ?? ""))
            .ToList();
        var blunders = blunderRows
            .Select(r => new FocusBlunder(r.GameId, r.Ply, r.San, r.Fen, r.Color, r.BestUci, r.BestSan, r.Side, r.Motif, r.Headline, r.Swing))
            .ToList();
        return (games, blunders);
    }

    private sealed class GameRow
    {
        public string Id { get; set; } = "";

        public string Date { get; set; } = "";

        public string? Opponent { get; set; }

        public string? Url { get; set; }
    }

    private sealed class BlunderRow
    {
        public string GameId { get; set; } = "";

        public int Ply { get; set; }

        public string San { get; set; } = "";

        public string Fen { get; set; } = "";

        public string Color { get; set; } = "";

        public string? BestUci { get; set; }

        public string? BestSan { get; set; }

        public string? Side { get; set; }

        public string? Motif { get; set; }

        public string? Headline { get; set; }

        public double? Swing { get; set; }
    }
}
