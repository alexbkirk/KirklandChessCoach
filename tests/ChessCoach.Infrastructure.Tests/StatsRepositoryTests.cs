using ChessCoach.Infrastructure.Data;

namespace ChessCoach.Infrastructure.Tests;

public sealed class StatsRepositoryTests
{
    [Fact]
    public async Task Games_load_oldest_first_with_their_stats_fields()
    {
        using var db = new TempDatabase();
        await db.Migrations.MigrateAsync();
        await db.ExecuteAsync(
            """
            INSERT INTO games(id, url, date, time, color, result, myRating, oppRating, timeControl, family, ending, plies, opponent) VALUES
              ('late', 'https://x/late', '2026-03-01', '09:00:00', 'black', 'draw', 1010, 1000, '900+10', 'Caro Kann Defense', 'agreement', 80, 'Eve'),
              ('early', NULL, '2025-12-31', '23:00:00', 'white', 'win', 990, 1000, '600', 'Pirc Defense', 'checkmate', 40, 'Bob');
            """);

        var games = await new StatsRepository(db.Connections).LoadGamesAsync();

        Assert.Equal(["early", "late"], games.Select(g => g.Id));
        Assert.Equal(new DateOnly(2025, 12, 31), games[0].Date);
        Assert.Equal("win", games[0].Result);
        Assert.Equal(1010, games[1].MyRating);
        Assert.Equal("900+10", games[1].TimeControl);
        Assert.Equal(80, games[1].Plies);
        Assert.Equal("https://x/late", games[1].Url);
        Assert.Equal("", games[0].Url);
    }

    [Fact]
    public async Task Move_samples_are_your_analysed_moves_except_the_last_of_each_game()
    {
        using var db = new TempDatabase();
        await db.Migrations.MigrateAsync();
        await db.ExecuteAsync(
            """
            INSERT INTO games(id, date, time) VALUES ('g1', '2026-10-10', '10:00:00');
            INSERT INTO moves(game_id, ply, color, is_mine, is_last, class, eval_before, cp_loss, clk) VALUES
              ('g1', 1, 'white', 1, 0, 'blunder', -120, 350, 42.5),
              ('g1', 3, 'white', 1, 0, 'good', 10, 5, NULL),
              ('g1', 4, 'black', 0, 0, 'blunder', 0, 300, 30),   -- the opponent's: left out
              ('g1', 5, 'white', 1, 1, 'blunder', 0, 300, 20);   -- final move: left out
            """);

        var samples = await new StatsRepository(db.Connections).LoadMoveSamplesAsync();

        Assert.Equal([1, 3], samples.Select(s => s.Ply));
        var blunder = samples[0];
        Assert.Equal(("g1", "white", -120, 350, true, 42.5), (blunder.GameId, blunder.Color, blunder.EvalBefore, blunder.CpLoss, blunder.IsBlunder, blunder.Clock));
        Assert.False(blunder.Balanced);
        Assert.False(samples[1].IsBlunder);
        Assert.Null(samples[1].Clock);
    }
}
