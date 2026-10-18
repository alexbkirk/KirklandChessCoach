using ChessCoach.Infrastructure.Data;

namespace ChessCoach.Infrastructure.Tests;

public sealed class FocusRepositoryTests
{
    [Fact]
    public async Task Every_non_final_blunder_of_yours_is_loaded_with_its_note_when_it_has_one()
    {
        using var db = new TempDatabase();
        await db.Migrations.MigrateAsync();
        await db.ExecuteAsync(
            """
            INSERT INTO games(id, url, date, time, opponent) VALUES ('g1', 'https://x/g1', '2026-10-10', '10:00:00', 'Dana');
            INSERT INTO moves(game_id, ply, color, is_mine, is_last, class, san, fen_before, best_uci, best_san, eval_before) VALUES
              ('g1', 5, 'white', 1, 0, 'blunder', 'Qh5', 'F5', 'd1d2', 'Qd2', -150),   -- a lost position: still loaded, the swing decides
              ('g1', 7, 'white', 1, 0, 'blunder', 'Bb4', 'F7', 'a5c5', 'Rxa5', -50),
              ('g1', 8, 'black', 0, 0, 'blunder', 'e5', 'F8', NULL, NULL, 0),          -- the opponent's: left out
              ('g1', 9, 'white', 1, 1, 'blunder', 'Kh1', 'F9', NULL, NULL, -50),      -- final move: left out
              ('g1', 11, 'white', 1, 0, 'mistake', 'a3', 'F11', NULL, NULL, 0);       -- not a blunder: left out
            INSERT INTO blunder_notes(game_id, ply, note) VALUES ('g1', 7,
              '{"side":"allowed","motif":{"k":"ignored_threat","l":"Ignored threat"},"headline":"Bb4 ignores a threat.","swing":58.4}');
            """);

        var (games, blunders) = await new FocusRepository(db.Connections).LoadAsync();

        var game = Assert.Single(games);
        Assert.Equal(new DateOnly(2026, 10, 10), game.Date);
        Assert.Equal("Dana", game.Opponent);
        Assert.Equal([5, 7], blunders.Select(b => b.Ply).Order());

        var noted = blunders.Single(b => b.Ply == 7);
        Assert.Equal(("allowed", "ignored_threat", "Bb4 ignores a threat.", 58.4), (noted.Side, noted.Motif, noted.Headline, noted.Swing));
        Assert.Equal(("Bb4", "F7", "white", "a5c5", "Rxa5"), (noted.San, noted.Fen, noted.Color, noted.BestUci, noted.BestSan));

        var unexplained = blunders.Single(b => b.Ply == 5);
        Assert.Null(unexplained.Swing);
        Assert.Null(unexplained.Motif);
    }
}
