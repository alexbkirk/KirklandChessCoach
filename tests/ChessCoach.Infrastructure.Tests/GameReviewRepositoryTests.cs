using ChessCoach.Core.Abstractions;
using ChessCoach.Infrastructure.Data;

namespace ChessCoach.Infrastructure.Tests;

public sealed class GameReviewRepositoryTests
{
    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = now;

        public DateOnly LocalToday => DateOnly.FromDateTime(UtcNow.UtcDateTime);
    }

    private static async Task<(TempDatabase Db, GameReviewRepository Repository, FixedClock Clock)> SetUpAsync()
    {
        var db = new TempDatabase();
        await db.Migrations.MigrateAsync();
        await db.ExecuteAsync(
            """
            INSERT INTO games(id, url, date, time, color, result, ending, timeControl, myRating, oppRating, opponent, family, opening)
              VALUES ('g1', 'https://x/g1', '2026-10-10', '10:00:00', 'white', 'loss', 'resignation', '600', 900, 950, 'Dana', 'Italian Game', 'Italian Game Giuoco Piano');
            INSERT INTO moves(game_id, ply, color, is_mine, is_last, san, uci, fen_before, best_uci, best_san, eval_before, eval_after, cp_loss, class, clk) VALUES
              ('g1', 2, 'black', 0, 0, 'e5', 'e7e5', 'F2', 'e7e5', 'e5', 20, 25, 0, 'best', 590.5),
              ('g1', 1, 'white', 1, 0, 'e4', 'e2e4', 'F1', 'e2e4', 'e4', 18, 20, 0, 'best', 598),
              ('g1', 3, 'white', 1, 1, 'Qh5', 'd1h5', 'F3', 'g1f3', 'Nf3', 25, -300, 325, 'blunder', NULL);
            """);
        var clock = new FixedClock(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero));
        return (db, new GameReviewRepository(db.Connections, clock), clock);
    }

    [Fact]
    public async Task A_game_loads_with_its_moves_in_order_and_the_analysis_fields()
    {
        var (db, repository, _) = await SetUpAsync();
        using var _1 = db;

        var review = await repository.LoadAsync("g1");

        Assert.NotNull(review);
        Assert.Equal(("Dana", "white", new DateOnly(2026, 10, 10), "Italian Game Giuoco Piano"), (review.Game.Opponent, review.Game.Color, review.Game.Date, review.Game.Opening));
        Assert.Equal([1, 2, 3], review.Moves.Select(m => m.Ply));
        var blunder = review.Moves[2];
        Assert.Equal(("Qh5", "d1h5", "Nf3", -300, 325, "blunder", true, true), (blunder.San, blunder.Uci, blunder.BestSan, blunder.EvalAfter, blunder.CpLoss, blunder.Class, blunder.IsMine, blunder.IsLast));
        Assert.Equal(598, review.Moves[0].Clock);
        Assert.Null(blunder.Clock);
    }

    [Fact]
    public async Task An_unknown_game_is_null()
    {
        var (db, repository, _) = await SetUpAsync();
        using var _1 = db;

        Assert.Null(await repository.LoadAsync("nope"));
    }

    [Fact]
    public async Task A_game_with_no_analysis_still_loads_with_no_moves()
    {
        var (db, repository, _) = await SetUpAsync();
        using var _1 = db;
        await db.ExecuteAsync("INSERT INTO games(id, date, time) VALUES ('g2', '2026-10-11', '09:00:00');");

        var review = await repository.LoadAsync("g2");

        Assert.NotNull(review);
        Assert.Empty(review.Moves);
        Assert.Empty(review.Annotations);
        Assert.Equal("", review.GameNote);
    }

    [Fact]
    public async Task A_move_annotation_is_saved_and_loaded_back()
    {
        var (db, repository, _) = await SetUpAsync();
        using var _1 = db;

        await repository.SaveMoveAnnotationAsync("g1", 3, "??", "Hung the queen.");

        var annotation = Assert.Single((await repository.LoadAsync("g1"))!.Annotations);
        Assert.Equal((3, "??", "Hung the queen."), (annotation.Ply, annotation.Glyph, annotation.Note));
    }

    [Fact]
    public async Task Saving_again_replaces_the_annotation_and_a_note_alone_has_no_symbol()
    {
        var (db, repository, clock) = await SetUpAsync();
        using var _1 = db;
        await repository.SaveMoveAnnotationAsync("g1", 3, "??", "first");

        clock.UtcNow = clock.UtcNow.AddHours(1);
        await repository.SaveMoveAnnotationAsync("g1", 3, null, "second");

        var annotation = Assert.Single((await repository.LoadAsync("g1"))!.Annotations);
        Assert.Equal((null, "second"), (annotation.Glyph, annotation.Note));
        Assert.Equal("2026-10-18T13:00:00.0000000+00:00", await db.ScalarAsync<string>("SELECT updated_at FROM move_annotations"));
    }

    [Fact]
    public async Task An_annotation_with_no_symbol_and_no_note_is_removed()
    {
        var (db, repository, _) = await SetUpAsync();
        using var _1 = db;
        await repository.SaveMoveAnnotationAsync("g1", 3, "?", "note");
        await repository.SaveMoveAnnotationAsync("g1", 1, "!", "");

        await repository.SaveMoveAnnotationAsync("g1", 3, null, "  ");

        Assert.Equal([1], (await repository.LoadAsync("g1"))!.Annotations.Select(a => a.Ply));
    }

    [Fact]
    public async Task The_game_note_is_saved_replaced_and_removed_when_emptied()
    {
        var (db, repository, _) = await SetUpAsync();
        using var _1 = db;

        await repository.SaveGameNoteAsync("g1", "Lost on time pressure.");
        await repository.SaveGameNoteAsync("g1", "Lost to a rushed queen move.");
        Assert.Equal("Lost to a rushed queen move.", (await repository.LoadAsync("g1"))!.GameNote);

        await repository.SaveGameNoteAsync("g1", "");
        Assert.Equal("", (await repository.LoadAsync("g1"))!.GameNote);
    }

    [Fact]
    public async Task Notes_are_kept_when_the_games_analysis_is_replaced()
    {
        var (db, repository, _) = await SetUpAsync();
        using var _1 = db;
        await repository.SaveMoveAnnotationAsync("g1", 3, "??", "Hung the queen.");
        await repository.SaveGameNoteAsync("g1", "Summary.");

        await db.ExecuteAsync("DELETE FROM moves WHERE game_id = 'g1';");

        var review = (await repository.LoadAsync("g1"))!;
        Assert.Empty(review.Moves);
        Assert.Single(review.Annotations);
        Assert.Equal("Summary.", review.GameNote);
    }

    [Fact]
    public async Task Notes_with_quotes_and_markup_round_trip_as_plain_text()
    {
        var (db, repository, _) = await SetUpAsync();
        using var _1 = db;
        const string tricky = "It's \"bad\"; <b>x</b>'); DROP TABLE moves;--";

        await repository.SaveMoveAnnotationAsync("g1", 2, null, tricky);

        Assert.Equal(tricky, (await repository.LoadAsync("g1"))!.Annotations.Single().Note);
        Assert.Equal(3, (await repository.LoadAsync("g1"))!.Moves.Count);
    }
}
