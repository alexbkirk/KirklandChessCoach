using ChessCoach.Core.Domain;
using ChessCoach.Infrastructure.Data;
using Dapper;

namespace ChessCoach.Infrastructure.Tests;

public sealed class PuzzleRepositoryTests
{
    private const string NoteJsonText = """
        {"kind":"missed_capture","headline":"Bxd8 wins a queen.","why":["Your bishop was left en prise."],
         "accept":["g5d8","e2e4"],"better":"Bxd8","better_line":"Bxd8 Nxc3","side":"missed","motif":{"k":"fork","l":"Knight fork"},
         "pattern":"Missed knight forks: 4 of these 100 blunders.",
         "maia":{"level":1100,"text":"Most players find this."},"eval_before":"+7.7","eval_after":"-6.2"}
        """;

    private static async Task SeedAsync(TempDatabase db)
    {
        await db.Migrations.MigrateAsync();
        await db.ExecuteAsync(
            """
            INSERT INTO games(id, url, date, time, opponent, timeClass) VALUES
              ('old', 'https://x/old', '2025-01-01', '10:00:00', 'Alice', 'rapid'),
              ('new', 'https://x/new', '2026-02-02', '09:00:00', 'Bob', 'rapid');
            INSERT INTO moves(game_id, ply, color, is_mine, san, uci, fen_before, best_uci, best_san, cp_loss, class) VALUES
              ('old', 17, 'white', 1, 'Nf3', 'g1f3', 'FEN_OLD w - - 0 9', 'g5d8', 'Bxd8', 300, 'blunder'),
              ('new', 9,  'black', 1, 'Qe7', 'd8e7', 'FEN_NEW b - - 0 5', 'c8g4', 'Bg4', 900, 'blunder');
            """);
    }

    private static async Task AddNoteAsync(TempDatabase db, string gameId, int ply, string json)
    {
        await db.ExecuteAsync($"INSERT INTO coach_notes(game_id, ply, note) VALUES ('{gameId}', {ply}, '{json.Replace("'", "''")}');");
    }

    [Fact]
    public async Task The_pool_is_ordered_costliest_first_and_parses_the_coach_note()
    {
        using var db = new TempDatabase();
        await SeedAsync(db);
        await AddNoteAsync(db, "old", 17, NoteJsonText);
        await AddNoteAsync(db, "new", 9, NoteJsonText);

        var pool = await new PuzzleRepository(db.Connections).LoadPoolAsync();

        Assert.Equal(["new_9", "old_17"], pool.Select(p => p.PositionId));
        var first = pool[0];
        Assert.Equal("FEN_NEW b - - 0 5", first.Fen);
        Assert.Equal("black", first.Color);
        Assert.Equal("c8g4", first.BestUci);
        Assert.Equal(["g5d8", "e2e4"], pool[1].Accept);
        Assert.Equal("Bxd8", pool[1].Note.BetterSan);
        Assert.Equal("Bxd8 Nxc3", pool[1].Note.BetterLine);
        Assert.Equal("Most players find this.", pool[1].Note.MaiaText);
        Assert.Equal("Missed knight fork", pool[1].Note.Motif);
        Assert.Equal("Missed knight forks: 4 of these 100 blunders.", pool[1].Note.Pattern);
        Assert.Equal("https://x/old", pool[1].Url);
        Assert.Equal("Alice", pool[1].Opponent);
    }

    [Fact]
    public async Task Notes_with_a_swing_come_first_biggest_swing_first()
    {
        using var db = new TempDatabase();
        await SeedAsync(db);
        await AddNoteAsync(db, "new", 9, """{"headline":"h","why":[],"swing":40.5,"side":"allowed","motif":{"k":"hung_piece","l":"Hung piece"}}""");
        await AddNoteAsync(db, "old", 17, """{"headline":"h","why":[],"swing":97.2,"side":"allowed","motif":{"k":"pin","l":"Pin"}}""");

        var pool = await new PuzzleRepository(db.Connections).LoadPoolAsync();

        Assert.Equal(["old_17", "new_9"], pool.Select(p => p.PositionId));
        Assert.Equal("Pin allowed", pool[0].Note.Motif);
        Assert.Equal("Hung piece", pool[1].Note.Motif);
        Assert.Equal("", pool[1].Note.Pattern);
    }

    [Fact]
    public async Task A_note_without_an_accept_list_falls_back_to_the_best_move()
    {
        using var db = new TempDatabase();
        await SeedAsync(db);
        await AddNoteAsync(db, "new", 9, """{"headline":"h","why":[],"tips":[]}""");

        var pool = await new PuzzleRepository(db.Connections).LoadPoolAsync();

        Assert.Equal(["c8g4"], pool.Single().Accept);
    }

    [Fact]
    public async Task Progress_round_trips_and_each_attempt_is_appended_to_the_history()
    {
        using var db = new TempDatabase();
        await SeedAsync(db);
        var repo = new PuzzleRepository(db.Connections);
        var now = new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);
        var first = new PuzzleProgress("new_9", 1, now.AddDays(1), 1, 1, now);

        await repo.RecordAttemptAsync(first, "c8g4", correct: true, gaveUp: false, hintUsed: false, now);
        var second = first with { Box = 0, DueUtc = now.AddMinutes(5), Tries = 2 };
        await repo.RecordAttemptAsync(second, "a1a2", correct: false, gaveUp: false, hintUsed: true, now.AddMinutes(1));

        var progress = await repo.LoadProgressAsync();
        var saved = progress["new_9"];
        Assert.Equal(0, saved.Box);
        Assert.Equal(now.AddMinutes(5), saved.DueUtc);
        Assert.Equal(2, saved.Tries);
        Assert.Equal(1, saved.Correct);
        Assert.Equal(2, await db.ScalarAsync<int>("SELECT COUNT(*) FROM puzzle_attempts WHERE position_id='new_9'"));
        Assert.Equal(1, await db.ScalarAsync<int>("SELECT hint_used FROM puzzle_attempts WHERE played_uci='a1a2'"));
    }
}
