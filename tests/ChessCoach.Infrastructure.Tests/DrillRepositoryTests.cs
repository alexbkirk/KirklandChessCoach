using ChessCoach.Core.Domain;
using ChessCoach.Infrastructure.Data;

namespace ChessCoach.Infrastructure.Tests;

public sealed class DrillRepositoryTests
{
    private static readonly DateOnly Day = new(2026, 10, 16);

    private static async Task SeedAsync(TempDatabase db)
    {
        await db.Migrations.MigrateAsync();
        await db.ExecuteAsync(
            """
            INSERT INTO games(id, url, date, opponent) VALUES ('g1', 'https://x/g1', '2026-01-01', 'Carol');
            INSERT INTO moves(game_id, ply, color, is_mine, is_last, san, uci, fen_before, best_san, eval_before, eval_after, cp_loss, class) VALUES
              ('g1', 11, 'white', 1, 0, 'Nf3', 'g1f3', 'FEN11', 'Nc3', 20, 10, 10, 'best'),
              ('g1', 13, 'white', 1, 0, 'Qh5', 'd1h5', 'FEN13', 'Nc3', 30, -400, 430, 'blunder'),
              ('g1', 15, 'white', 1, 0, 'Bb5', 'f1b5', 'FEN15', 'Bb5', 900, 900, 0, 'best'),
              ('g1', 17, 'white', 1, 1, 'Kh1', 'e1h1', 'FEN17', 'Kh1', 0, 0, 0, 'best');
            INSERT INTO blunder_tags(game_id, ply, tags) VALUES ('g1', 13, '[{"k":"one_move","l":"One-move blunder"}]');
            """);
    }

    [Fact]
    public async Task Candidates_are_one_move_blunders_and_balanced_sound_moves()
    {
        using var db = new TempDatabase();
        await SeedAsync(db);
        var repo = new DrillRepository(db.Connections);

        var (blunders, safe) = await repo.CountCandidatesAsync();
        var blunder = await repo.GetCandidateAsync(blunder: true, 0);
        var sound = await repo.GetCandidateAsync(blunder: false, 0);

        Assert.Equal(1, blunders);
        Assert.Equal(1, safe);   // ply 15 is not balanced (+9.0) and ply 17 is the last move
        Assert.Equal("g1_13", blunder!.PositionId);
        Assert.True(blunder.IsBlunder);
        Assert.Equal("Carol", blunder.Opponent);
        Assert.Equal("g1_11", sound!.PositionId);
        Assert.False(sound.IsBlunder);
        Assert.Null(await repo.GetCandidateAsync(blunder: true, 5));
    }

    [Fact]
    public async Task A_blunder_carries_its_note_and_a_position_without_one_has_none()
    {
        using var db = new TempDatabase();
        await SeedAsync(db);
        await db.ExecuteAsync(
            """
            INSERT INTO blunder_notes(game_id, ply, note) VALUES ('g1', 13,
              '{"side":"allowed","motif":{"k":"fork","l":"Knight fork"},"headline":"Qh5 allows a knight fork.","why":["Winning chances: 52% before, 8% after."],"better":"Nc3","better_line":"Nc3 Nf6"}');
            """);
        var repo = new DrillRepository(db.Connections);

        var blunder = await repo.GetCandidateAsync(blunder: true, 0);
        var sound = await repo.GetCandidateAsync(blunder: false, 0);

        var note = blunder!.Note!;
        Assert.Equal("Qh5 allows a knight fork.", note.Headline);
        Assert.Equal("Knight fork allowed", note.Motif);
        Assert.Equal(["Winning chances: 52% before, 8% after."], note.Why);
        Assert.Equal("Nc3", note.BetterSan);
        Assert.Equal("Nc3 Nf6", note.BetterLine);
        Assert.Null(sound!.Note);

        await repo.SaveNewDayAsync(Day, 1, [new DrillSlot { Index = 0, Format = DrillFormat.Safe, Position = blunder }]);
        Assert.Equal("Qh5 allows a knight fork.", (await repo.LoadDayAsync(Day))!.Single().Position.Note!.Headline);
    }

    [Fact]
    public async Task A_day_round_trips_with_slot_state_and_completed_days_need_every_slot_done()
    {
        using var db = new TempDatabase();
        await SeedAsync(db);
        var repo = new DrillRepository(db.Connections);
        var position = (await repo.GetCandidateAsync(true, 0))!;
        var slots = new List<DrillSlot>
        {
            new() { Index = 0, Format = DrillFormat.Cct, Position = position },
            new() { Index = 1, Format = DrillFormat.Safe, Position = position },
        };

        Assert.Null(await repo.LoadDayAsync(Day));
        await repo.SaveNewDayAsync(Day, 42, slots);
        slots[0].Round = 1;
        slots[0].Score = 1;
        slots[0].Answers.Add(new DrillAnswer(true, ["f7"], [], []));
        await repo.SaveSlotAsync(Day, slots[0]);

        var loaded = (await repo.LoadDayAsync(Day))!;
        Assert.Equal(2, loaded.Count);
        Assert.Equal(DrillFormat.Cct, loaded[0].Format);
        Assert.Equal(1, loaded[0].Round);
        Assert.Equal(["f7"], loaded[0].Answers.Single().Right);
        Assert.Equal("Qh5", loaded[0].Position.San);
        Assert.Empty(await repo.LoadCompletedDaysAsync());

        foreach (var slot in slots)
        {
            slot.Done = true;
            await repo.SaveSlotAsync(Day, slot);
        }

        Assert.Equal([Day], await repo.LoadCompletedDaysAsync());
    }
}
