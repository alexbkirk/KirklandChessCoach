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
}
