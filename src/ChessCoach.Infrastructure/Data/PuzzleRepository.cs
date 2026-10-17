using System.Globalization;
using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Domain;
using Dapper;

namespace ChessCoach.Infrastructure.Data;

public sealed class PuzzleRepository(SqliteConnectionFactory connections) : IPuzzleRepository
{
    public async Task<IReadOnlyList<Puzzle>> LoadPoolAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        // Most winning chances lost first (notes from before the swing field fall back to centipawn loss), then most recent game.
        var rows = await connection.QueryAsync<PoolRow>(new CommandDefinition(
            """
            SELECT n.game_id AS GameId, n.ply AS Ply, n.note AS Note,
                   m.fen_before AS Fen, m.color AS Color, m.san AS PlayedSan, m.best_uci AS BestUci, m.cp_loss AS CpLoss,
                   CAST(g.date AS TEXT) AS Date, g.opponent AS Opponent, g.url AS Url
            FROM coach_notes n
            JOIN moves m ON m.game_id = n.game_id AND m.ply = n.ply
            JOIN games g ON g.id = n.game_id
            ORDER BY json_extract(n.note, '$.swing') DESC, m.cp_loss DESC, CAST(g.date AS TEXT) DESC, g.time DESC
            """,
            cancellationToken: cancellationToken));
        return rows.Select(ToPuzzle).ToList();
    }

    public async Task<IReadOnlyDictionary<string, PuzzleProgress>> LoadProgressAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<ProgressRow>(new CommandDefinition(
            "SELECT position_id AS PositionId, box AS Box, due_utc AS DueUtc, tries AS Tries, correct AS Correct, last_seen_utc AS LastSeenUtc FROM puzzle_progress",
            cancellationToken: cancellationToken));
        return rows.ToDictionary(r => r.PositionId, r => new PuzzleProgress(
            r.PositionId, r.Box, Parse(r.DueUtc), r.Tries, r.Correct, r.LastSeenUtc is null ? null : Parse(r.LastSeenUtc)));
    }

    public async Task RecordAttemptAsync(PuzzleProgress progress, string playedUci, bool correct, bool gaveUp, bool hintUsed, DateTimeOffset atUtc, CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(
            """
            INSERT INTO puzzle_progress(position_id, box, due_utc, tries, correct, last_seen_utc)
            VALUES (@PositionId, @Box, @DueUtc, @Tries, @Correct, @LastSeenUtc)
            ON CONFLICT(position_id) DO UPDATE SET
              box = excluded.box, due_utc = excluded.due_utc, tries = excluded.tries,
              correct = excluded.correct, last_seen_utc = excluded.last_seen_utc
            """,
            new
            {
                progress.PositionId,
                progress.Box,
                DueUtc = Format(progress.DueUtc),
                progress.Tries,
                progress.Correct,
                LastSeenUtc = progress.LastSeenUtc is null ? null : Format(progress.LastSeenUtc.Value),
            },
            transaction);
        await connection.ExecuteAsync(
            """
            INSERT INTO puzzle_attempts(position_id, played_uci, correct, gave_up, hint_used, at_utc)
            VALUES (@PositionId, @PlayedUci, @Correct, @GaveUp, @HintUsed, @AtUtc)
            """,
            new
            {
                progress.PositionId,
                PlayedUci = playedUci,
                Correct = correct ? 1 : 0,
                GaveUp = gaveUp ? 1 : 0,
                HintUsed = hintUsed ? 1 : 0,
                AtUtc = Format(atUtc),
            },
            transaction);
        await transaction.CommitAsync(cancellationToken);
    }

    private static Puzzle ToPuzzle(PoolRow row)
    {
        var note = NoteJson.Parse(row.Note)
            ?? throw new InvalidDataException($"Coach note for {row.GameId} ply {row.Ply} is empty.");
        return new Puzzle(
            PositionId: $"{row.GameId}_{row.Ply}",
            GameId: row.GameId,
            Ply: row.Ply,
            Fen: row.Fen,
            Color: row.Color,
            PlayedSan: row.PlayedSan,
            BestUci: row.BestUci,
            Accept: note.Accept ?? [row.BestUci],
            CpLoss: row.CpLoss,
            Date: row.Date,
            Opponent: row.Opponent,
            Url: row.Url,
            Note: note.ToCoachNote());
    }

    private static string Format(DateTimeOffset value) => value.ToString("o", CultureInfo.InvariantCulture);

    private static DateTimeOffset Parse(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private sealed class PoolRow
    {
        public string GameId { get; set; } = "";

        public int Ply { get; set; }

        public string Note { get; set; } = "";

        public string Fen { get; set; } = "";

        public string Color { get; set; } = "";

        public string PlayedSan { get; set; } = "";

        public string BestUci { get; set; } = "";

        public int CpLoss { get; set; }

        public string Date { get; set; } = "";

        public string Opponent { get; set; } = "";

        public string Url { get; set; } = "";
    }

    private sealed class ProgressRow
    {
        public string PositionId { get; set; } = "";

        public int Box { get; set; }

        public string DueUtc { get; set; } = "";

        public int Tries { get; set; }

        public int Correct { get; set; }

        public string? LastSeenUtc { get; set; }
    }
}
