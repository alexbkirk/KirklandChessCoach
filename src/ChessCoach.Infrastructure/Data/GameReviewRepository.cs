using System.Globalization;
using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Review;
using Dapper;

namespace ChessCoach.Infrastructure.Data;

public sealed class GameReviewRepository(SqliteConnectionFactory connections, IClock clock) : IGameReviewRepository
{
    public async Task<GameReview?> LoadAsync(string gameId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);

        // Untyped columns in the Python-created tables: cast text, and map through settable properties (see ARCHITECTURE.md).
        var game = await connection.QuerySingleOrDefaultAsync<GameRow>(new CommandDefinition(
            """
            SELECT CAST(id AS TEXT) AS Id, CAST(url AS TEXT) AS Url, CAST(date AS TEXT) AS Date, CAST(color AS TEXT) AS Color,
                   CAST(result AS TEXT) AS Result, CAST(ending AS TEXT) AS Ending, CAST(timeControl AS TEXT) AS TimeControl,
                   myRating AS MyRating, oppRating AS OppRating, CAST(opponent AS TEXT) AS Opponent,
                   CAST(family AS TEXT) AS Family, CAST(opening AS TEXT) AS Opening
            FROM games WHERE id = @gameId
            """,
            new { gameId },
            cancellationToken: cancellationToken));
        if (game is null) return null;

        var moves = await connection.QueryAsync<MoveRow>(new CommandDefinition(
            """
            SELECT ply AS Ply, CAST(color AS TEXT) AS Color, is_mine AS IsMine, is_last AS IsLast, CAST(san AS TEXT) AS San,
                   CAST(uci AS TEXT) AS Uci, CAST(fen_before AS TEXT) AS FenBefore, CAST(best_uci AS TEXT) AS BestUci,
                   CAST(best_san AS TEXT) AS BestSan, COALESCE(eval_before, 0) AS EvalBefore, COALESCE(eval_after, 0) AS EvalAfter,
                   COALESCE(cp_loss, 0) AS CpLoss, COALESCE(CAST(class AS TEXT), '') AS Class, clk AS Clock
            FROM moves WHERE game_id = @gameId ORDER BY ply
            """,
            new { gameId },
            cancellationToken: cancellationToken));

        var annotations = await connection.QueryAsync<AnnotationRow>(new CommandDefinition(
            "SELECT ply AS Ply, glyph AS Glyph, note AS Note FROM move_annotations WHERE game_id = @gameId ORDER BY ply",
            new { gameId },
            cancellationToken: cancellationToken));

        var gameNote = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT note FROM game_review_notes WHERE game_id = @gameId",
            new { gameId },
            cancellationToken: cancellationToken));

        return new GameReview(
            Game: new ReviewGame(
                Id: game.Id,
                Url: game.Url ?? "",
                Date: DateOnly.ParseExact(game.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                Color: game.Color ?? "",
                Result: game.Result ?? "",
                Ending: game.Ending ?? "",
                TimeControl: game.TimeControl ?? "",
                MyRating: game.MyRating,
                OppRating: game.OppRating,
                Opponent: game.Opponent ?? "",
                Family: game.Family ?? "",
                Opening: game.Opening ?? ""),
            Moves: moves.Select(m => new ReviewMove(
                Ply: m.Ply,
                Color: m.Color ?? "",
                IsMine: m.IsMine != 0,
                IsLast: m.IsLast != 0,
                San: m.San ?? "",
                Uci: m.Uci ?? "",
                FenBefore: m.FenBefore ?? "",
                BestUci: m.BestUci,
                BestSan: m.BestSan,
                EvalBefore: m.EvalBefore,
                EvalAfter: m.EvalAfter,
                CpLoss: m.CpLoss,
                Class: m.Class,
                Clock: m.Clock)).ToList(),
            Annotations: annotations.Select(a => new MoveAnnotation(a.Ply, string.IsNullOrEmpty(a.Glyph) ? null : a.Glyph, a.Note ?? "")).ToList(),
            GameNote: gameNote ?? "");
    }

    public async Task SaveMoveAnnotationAsync(string gameId, int ply, string? glyph, string note, CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        if (string.IsNullOrEmpty(glyph) && string.IsNullOrWhiteSpace(note))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM move_annotations WHERE game_id = @gameId AND ply = @ply",
                new { gameId, ply },
                cancellationToken: cancellationToken));
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO move_annotations(game_id, ply, glyph, note, updated_at) VALUES (@gameId, @ply, @glyph, @note, @at)
            ON CONFLICT(game_id, ply) DO UPDATE SET glyph = excluded.glyph, note = excluded.note, updated_at = excluded.updated_at
            """,
            new { gameId, ply, glyph = string.IsNullOrEmpty(glyph) ? null : glyph, note, at = Now() },
            cancellationToken: cancellationToken));
    }

    public async Task SaveGameNoteAsync(string gameId, string note, CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(note))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM game_review_notes WHERE game_id = @gameId",
                new { gameId },
                cancellationToken: cancellationToken));
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO game_review_notes(game_id, note, updated_at) VALUES (@gameId, @note, @at)
            ON CONFLICT(game_id) DO UPDATE SET note = excluded.note, updated_at = excluded.updated_at
            """,
            new { gameId, note, at = Now() },
            cancellationToken: cancellationToken));
    }

    private string Now() => clock.UtcNow.ToString("O", CultureInfo.InvariantCulture);

    private sealed class GameRow
    {
        public string Id { get; set; } = "";

        public string? Url { get; set; }

        public string Date { get; set; } = "";

        public string? Color { get; set; }

        public string? Result { get; set; }

        public string? Ending { get; set; }

        public string? TimeControl { get; set; }

        public int MyRating { get; set; }

        public int OppRating { get; set; }

        public string? Opponent { get; set; }

        public string? Family { get; set; }

        public string? Opening { get; set; }
    }

    private sealed class MoveRow
    {
        public int Ply { get; set; }

        public string? Color { get; set; }

        public int IsMine { get; set; }

        public int IsLast { get; set; }

        public string? San { get; set; }

        public string? Uci { get; set; }

        public string? FenBefore { get; set; }

        public string? BestUci { get; set; }

        public string? BestSan { get; set; }

        public int EvalBefore { get; set; }

        public int EvalAfter { get; set; }

        public int CpLoss { get; set; }

        public string Class { get; set; } = "";

        public double? Clock { get; set; }
    }

    private sealed class AnnotationRow
    {
        public int Ply { get; set; }

        public string? Glyph { get; set; }

        public string? Note { get; set; }
    }
}
