using ChessCoach.Core.Stats;
using ChessCoach.Infrastructure.Data;

namespace ChessCoach.Infrastructure.Tests;

public sealed class OpponentRepositoryTests
{
    private static async Task SeedAsync(TempDatabase db)
    {
        await db.Migrations.MigrateAsync();
        await db.ExecuteAsync(
            """
            INSERT INTO games(id, url, date, time, color, result, ending, myRating, oppRating, opponent, family, opening) VALUES
              ('g1', 'https://x/g1', '2025-01-01', '10:00:00', 'white', 'win', 'resignation', 1000, 1100, 'Rival', 'Caro Kann Defense', 'Caro Kann Defense Advance'),
              ('g2', 'https://x/g2', '2025-02-01', '10:00:00', 'black', 'loss', 'checkmate', 990, 1120, 'rival', 'Pirc Defense', 'Pirc Defense'),
              ('g3', 'https://x/g3', '2025-03-01', '10:00:00', 'white', 'win', 'timeout', 1000, 900, 'Someone', 'Pirc Defense', 'Pirc Defense');
            INSERT INTO moves(game_id, ply, color, is_mine, san, uci, fen_before, best_san, best_uci, cp_loss, class, is_last, clk) VALUES
              ('g1', 1, 'white', 1, 'e4', 'e2e4', 'F1', 'e4', 'e2e4', 0, 'best', 0, 598.5),
              ('g1', 2, 'black', 0, 'c6', 'c7c6', 'F2', 'c6', 'c7c6', 10, 'best', 0, 597),
              ('g1', 61, 'white', 1, 'Qh5', 'd1h5', 'F61', 'Qd1', 'd1d1', 400, 'blunder', 1, 30),
              ('g2', 1, 'white', 0, 'e4', 'e2e4', 'F1', 'e4', 'e2e4', 0, 'best', 0, 600),
              ('g3', 1, 'white', 1, 'e4', 'e2e4', 'F1', 'e4', 'e2e4', 0, 'best', 0, 600),
              ('g3', 2, 'black', 0, 'g6', 'g7g6', 'F2b', 'e5', 'e7e5', 300, 'blunder', 0, 600);
            INSERT INTO blunder_notes(game_id, ply, note) VALUES ('g1', 61, '{"headline":"Qh5 hangs a piece."}');
            """);
    }

    [Fact]
    public async Task An_opponent_is_matched_without_case_and_brings_every_move_with_clock_and_note()
    {
        using var db = new TempDatabase();
        await SeedAsync(db);

        var (games, plies) = await new OpponentRepository(db.Connections).LoadAsync("RIVAL");

        Assert.Equal(["g1", "g2"], games.Select(g => g.GameId).Order(StringComparer.Ordinal));
        var g2 = games.Single(g => g.GameId == "g2");
        Assert.Equal(("black", "loss", "checkmate", 990, 1120, "Pirc Defense", "https://x/g2", "rival"), (g2.Color, g2.Result, g2.Ending, g2.MyRating, g2.OppRating, g2.Family, g2.Url, g2.Opponent));
        Assert.Equal(4, plies.Count);
        var blunder = plies.Single(p => p.Ply == 61);
        Assert.True(blunder.IsMine);
        Assert.True(blunder.IsLast);
        Assert.Equal(30, blunder.Clock);
        Assert.Equal("Qh5 hangs a piece.", blunder.NoteHeadline);
        Assert.Null(plies.Single(p => p.GameId == "g1" && p.Ply == 2).NoteHeadline);
    }

    [Fact]
    public async Task Names_with_spaces_quotes_and_symbols_are_matched_exactly_and_safely()
    {
        using var db = new TempDatabase();
        await db.Migrations.MigrateAsync();
        await db.ExecuteAsync(
            """
            INSERT INTO games(id, url, date, time, color, result, ending, myRating, oppRating, opponent, family, opening) VALUES
              ('s1', 'u1', '2025-01-01', '10:00:00', 'white', 'win', 'resignation', 1000, 1000, 'O''Brien_99 x', 'Pirc Defense', 'Pirc Defense'),
              ('s2', 'u2', '2025-01-02', '10:00:00', 'white', 'win', 'resignation', 1000, 1000, 'OBrien_99 x', 'Pirc Defense', 'Pirc Defense');
            """);
        var repo = new OpponentRepository(db.Connections);

        var (games, _) = await repo.LoadAsync("o'brien_99 X");
        var (injection, _) = await repo.LoadAsync("x' OR '1'='1");

        Assert.Equal(["s1"], games.Select(g => g.GameId));
        Assert.Empty(injection);
    }

    [Fact]
    public async Task The_baseline_covers_all_games_per_side_and_phase_without_final_moves()
    {
        using var db = new TempDatabase();
        await SeedAsync(db);

        var baseline = await new OpponentRepository(db.Connections).LoadBaselineAsync();

        var you = baseline.You[OpponentCalculator.Opening];
        Assert.Equal(2, you.Moves);              // g1 e4 and g3 e4; the final Qh5 is left out
        Assert.Equal(0, you.AvgCpLoss);
        var them = baseline.Opponents[OpponentCalculator.Opening];
        Assert.Equal(3, them.Moves);
        Assert.Equal(310.0 / 3, them.AvgCpLoss, 6);
        Assert.Equal(100.0 / 3, them.BlunderPercent, 6);
        Assert.False(baseline.You.ContainsKey(OpponentCalculator.Late));
    }
}
