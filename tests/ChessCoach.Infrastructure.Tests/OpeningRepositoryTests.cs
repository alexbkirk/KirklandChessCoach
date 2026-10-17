using ChessCoach.Core.Domain;
using ChessCoach.Infrastructure.Data;

namespace ChessCoach.Infrastructure.Tests;

public sealed class OpeningRepositoryTests
{
    [Fact]
    public async Task Games_carry_blunders_the_first_slip_and_the_evaluation_from_the_players_side()
    {
        using var db = new TempDatabase();
        await db.Migrations.MigrateAsync();
        await db.ExecuteAsync(
            """
            INSERT INTO games(id, color, family, opening, result) VALUES
              ('w1', 'white', 'Caro Kann Defense', 'Caro Kann Defense Advance', 'win'),
              ('b1', 'black', 'Caro Kann Defense', 'Caro Kann Defense Advance', 'loss');
            INSERT INTO moves(game_id, ply, color, is_mine, is_last, class, cp_loss, eval_after) VALUES
              ('w1', 3, 'white', 1, 0, 'mistake', 150, 0),     -- my move 2: first slip (150 cp)
              ('w1', 5, 'white', 1, 0, 'blunder', 300, 0),
              ('w1', 19, 'white', 1, 0, 'best', 0, 250),       -- my move 10: +2.50 for White = +250 for me
              ('b1', 4, 'black', 1, 0, 'best', 0, 0),
              ('b1', 20, 'black', 1, 0, 'best', 0, 250),       -- my move 10 as Black: White +2.50 is -2.50 for me
              ('b1', 21, 'black', 1, 1, 'blunder', 900, 0);    -- final move: not counted as a blunder
            """);

        var games = (await new OpeningRepository(db.Connections).LoadAsync()).ToDictionary(g => g.Id);

        var white = games["w1"];
        Assert.True(white.Won);
        Assert.Equal(1, white.Blunders);          // ply 3 is a mistake, ply 5 the only blunder
        Assert.Equal(2, white.FirstSlipMove);     // ply 3 = my move 2
        Assert.Equal(250, white.EvalAt10);

        var black = games["b1"];
        Assert.False(black.Won);
        Assert.Equal(0, black.Blunders);
        Assert.Null(black.FirstSlipMove);
        Assert.Equal(-250, black.EvalAt10);
    }

    private static async Task SeedVariationAsync(TempDatabase db)
    {
        await db.Migrations.MigrateAsync();
        await db.ExecuteAsync(
            """
            INSERT INTO games(id, color, family, opening, result, date, opponent, url) VALUES
              ('b1', 'black', 'Caro Kann Defense', 'Caro Kann Defense Advance', 'loss', '2026-01-02', 'Ann', 'https://x/b1'),
              ('b2', 'black', 'Caro Kann Defense', 'Caro Kann Defense Exchange', 'win', '2026-01-03', 'Bo', 'https://x/b2'),
              ('w1', 'white', 'Caro Kann Defense', 'Caro Kann Defense Advance', 'win', '2026-01-04', 'Cy', 'https://x/w1');
            INSERT INTO moves(game_id, ply, color, is_mine, san, uci, fen_before, best_san, best_uci, cp_loss) VALUES
              ('b1', 1, 'white', 0, 'e4', 'e2e4', 'F1', 'e4', 'e2e4', 0),
              ('b1', 2, 'black', 1, 'c6', 'c7c6', 'F2', 'c6', 'c7c6', 10),
              ('b1', 31, 'white', 0, 'Kh1', 'g1h1', 'F31', 'Kh1', 'g1h1', 0),   -- after move 15: left out
              ('b2', 1, 'white', 0, 'e4', 'e2e4', 'F1', 'e4', 'e2e4', 0),
              ('w1', 1, 'white', 1, 'e4', 'e2e4', 'F1', 'd4', 'd2d4', 40);
            """);
    }

    [Fact]
    public async Task A_variation_loads_its_games_and_their_first_fifteen_moves_for_one_colour()
    {
        using var db = new TempDatabase();
        await SeedVariationAsync(db);
        var repo = new OpeningRepository(db.Connections);

        var (games, plies) = await repo.LoadVariationAsync(new OpeningScope("black", null, "Caro Kann Defense Advance"));

        var game = Assert.Single(games);
        Assert.Equal(new VariationGame("b1", "loss", "2026-01-02", "Ann", "https://x/b1"), game);
        Assert.Equal([1, 2], plies.Select(p => p.Ply));
        Assert.Equal(new VariationPly("b1", 2, "F2", "c6", "c7c6", true, "c6", "c7c6", 10), plies[1]);
        Assert.False(plies[0].IsMine);
    }

    [Fact]
    public async Task A_family_scope_covers_every_variation_in_it()
    {
        using var db = new TempDatabase();
        await SeedVariationAsync(db);

        var (games, _) = await new OpeningRepository(db.Connections).LoadVariationAsync(new OpeningScope("black", "Caro Kann Defense", null));

        Assert.Equal(["b1", "b2"], games.Select(g => g.GameId).Order());
    }

    [Fact]
    public async Task Practice_history_counts_tries_and_remembers_the_latest_answer()
    {
        using var db = new TempDatabase();
        await db.Migrations.MigrateAsync();
        var repo = new OpeningRepository(db.Connections);
        var at = new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

        await repo.RecordPracticeAsync("k1", "g8f6", correct: false, at);
        await repo.RecordPracticeAsync("k1", "d5e4", correct: true, at.AddMinutes(1));
        await repo.RecordPracticeAsync("k2", "e7e6", correct: false, at);
        await repo.RecordPracticeAsync("other", "a7a6", correct: true, at);

        var tally = await repo.LoadPracticeAsync(["k1", "k2", "never"]);

        Assert.Equal(new PracticeTally(2, 1, true), tally["k1"]);
        Assert.Equal(new PracticeTally(1, 0, false), tally["k2"]);
        Assert.Equal(2, tally.Count);
        Assert.Empty(await repo.LoadPracticeAsync([]));
    }
}
