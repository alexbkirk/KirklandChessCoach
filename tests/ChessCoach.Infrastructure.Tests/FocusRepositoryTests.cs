using ChessCoach.Infrastructure.Data;

namespace ChessCoach.Infrastructure.Tests;

public sealed class FocusRepositoryTests
{
    [Fact]
    public async Task Only_balanced_non_final_blunders_are_loaded_with_their_tags()
    {
        using var db = new TempDatabase();
        await db.Migrations.MigrateAsync();
        await db.ExecuteAsync(
            """
            INSERT INTO games(id, url, date, time, opponent) VALUES ('g1', 'https://x/g1', '2026-10-10', '10:00:00', 'Dana');
            INSERT INTO moves(game_id, ply, color, is_mine, is_last, class, eval_before) VALUES
              ('g1', 5, 'white', 1, 0, 'blunder', -150),   -- lost position: excluded
              ('g1', 7, 'white', 1, 0, 'blunder', -50),    -- balanced: included
              ('g1', 9, 'white', 1, 1, 'blunder', -50);    -- final move: excluded
            INSERT INTO blunder_tags(game_id, ply, tags) VALUES
              ('g1', 5, '[{"k":"missed_mate","l":"Missed mate"}]'),
              ('g1', 7, '[{"k":"missed_tactic","l":"Missed tactic"},{"k":"one_move","l":"One-move blunder"}]'),
              ('g1', 9, '[{"k":"other","l":"Positional slip"}]');
            """);

        var (games, blunders) = await new FocusRepository(db.Connections).LoadAsync();

        var game = Assert.Single(games);
        Assert.Equal(new DateOnly(2026, 10, 10), game.Date);
        Assert.Equal("Dana", game.Opponent);
        var blunder = Assert.Single(blunders);
        Assert.Equal(7, blunder.Ply);
        Assert.Equal(["missed_tactic", "one_move"], blunder.Tags);
    }
}
