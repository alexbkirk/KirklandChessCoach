using ChessCoach.Core.Domain;
using ChessCoach.Infrastructure.Data;

namespace ChessCoach.Infrastructure.Tests;

public sealed class TournamentRepositoryTests
{
    [Fact]
    public async Task Games_lists_only_games_with_a_tournament_newest_first()
    {
        using var db = new TempDatabase();
        await db.Migrations.MigrateAsync();
        await db.ExecuteAsync(
            """
            INSERT INTO games(id, date, time, color, result, myRating, oppRating, timeControl, family, ending, plies, opponent, timeClass, tournament) VALUES
              ('early', '2025-12-31', '23:00:00', 'white', 'win', 990, 1000, '600', 'Pirc Defense', 'checkmate', 40, 'Bob', 'rapid', 'Open A'),
              ('late', '2026-03-01', '09:00:00', 'black', 'draw', 1010, 1000, '900+10', 'Caro Kann Defense', 'agreement', 80, 'Eve', 'blitz', 'Open A'),
              ('online', '2026-04-01', '09:00:00', 'black', 'draw', 1010, 1000, '900+10', 'Caro Kann Defense', 'agreement', 80, 'Eve', 'blitz', NULL);
            """);

        var games = await new TournamentRepository(db.Connections).LoadGamesAsync();

        Assert.Equal(["late", "early"], games.Select(g => g.Id));
    }

    [Fact]
    public async Task Games_ignore_the_time_class_of_each_game()
    {
        using var db = new TempDatabase();
        await db.Migrations.MigrateAsync();
        await db.ExecuteAsync(
            """
            INSERT INTO games(id, date, time, color, result, timeClass, tournament) VALUES
              ('blitz-event', '2026-05-01', '10:00:00', 'white', 'win', 'blitz', 'Open B'),
              ('rapid-event', '2026-05-02', '10:00:00', 'white', 'loss', 'rapid', 'Open B');
            """);

        var games = await new TournamentRepository(db.Connections).LoadGamesAsync();

        Assert.Equal(["rapid-event", "blitz-event"], games.Select(g => g.Id));
    }

    [Fact]
    public async Task Move_samples_come_from_tournament_games_only()
    {
        using var db = new TempDatabase();
        await db.Migrations.MigrateAsync();
        await db.ExecuteAsync(
            """
            INSERT INTO games(id, date, time, tournament) VALUES
              ('t1', '2026-10-10', '10:00:00', 'Open A'),
              ('o1', '2026-10-11', '10:00:00', NULL);
            INSERT INTO moves(game_id, ply, color, is_mine, is_last, class, eval_before, cp_loss, clk) VALUES
              ('t1', 1, 'white', 1, 0, 'blunder', -120, 350, 42.5),
              ('t1', 2, 'black', 0, 0, 'good', 0, 0, 30),
              ('t1', 3, 'white', 1, 1, 'good', 0, 0, 20),
              ('o1', 1, 'white', 1, 0, 'blunder', 0, 300, 10);
            """);

        var samples = await new TournamentRepository(db.Connections).LoadMoveSamplesAsync();

        Assert.Equal(["t1"], samples.Select(s => s.GameId).Distinct());
        Assert.Equal([1], samples.Select(s => s.Ply));
        Assert.True(samples[0].IsBlunder);
    }

    [Fact]
    public async Task Added_manual_game_is_listed_afterwards()
    {
        using var db = new TempDatabase();
        await db.Migrations.MigrateAsync();
        var repository = new TournamentRepository(db.Connections);
        var game = MakeGame(Sans:["e4", "e5", "Nf3"]);

        await repository.AddManualGameAsync(game);

        var games = await repository.LoadGamesAsync();
        Assert.Equal([game.Id], games.Select(g => g.Id));
        Assert.Equal(new DateOnly(2026, 10, 18), games[0].Date);
        Assert.Equal("white", games[0].Color);
        Assert.Equal("win", games[0].Result);
        Assert.Equal(3, games[0].Plies);
        Assert.Equal("Carol", games[0].Opponent);
        Assert.Equal(1500, games[0].MyRating);
        Assert.Equal("-", games[0].TimeControl);
        Assert.Equal("Unknown", games[0].Family);
        Assert.Equal("other", games[0].Ending);
        Assert.Equal("", games[0].Url);
        Assert.Equal("00:00:00", games[0].Time);
    }

    [Fact]
    public async Task Adding_the_same_manual_game_twice_leaves_one_row()
    {
        using var db = new TempDatabase();
        await db.Migrations.MigrateAsync();
        var repository = new TournamentRepository(db.Connections);
        var game = MakeGame(Sans:["d4", "d5"]);

        await repository.AddManualGameAsync(game);
        await repository.AddManualGameAsync(game);

        Assert.Equal(1, await db.ScalarAsync<long>($"SELECT COUNT(*) FROM games WHERE id = '{game.Id}'"));
    }

    [Fact]
    public async Task Adding_a_manual_game_adds_no_analysis_rows()
    {
        using var db = new TempDatabase();
        await db.Migrations.MigrateAsync();
        var repository = new TournamentRepository(db.Connections);

        await repository.AddManualGameAsync(MakeGame(Sans:["e4", "c5"]));

        Assert.Equal(0, await db.ScalarAsync<long>("SELECT COUNT(*) FROM moves"));
    }

    [Fact]
    public async Task Manual_game_tournament_key_is_otb_with_its_event()
    {
        using var db = new TempDatabase();
        await db.Migrations.MigrateAsync();
        var repository = new TournamentRepository(db.Connections);
        var game = MakeGame(Sans:["e4", "e5"]);

        await repository.AddManualGameAsync(game);

        var keys = await repository.LoadTournamentKeysAsync();
        Assert.Equal("otb:Club Open", keys[game.Id]);
    }

    [Fact]
    public async Task Manual_game_event_is_trimmed_in_tournament_key()
    {
        using var db = new TempDatabase();
        await db.Migrations.MigrateAsync();
        var repository = new TournamentRepository(db.Connections);
        var game = new ManualGame(
            Date: new DateOnly(2026, 10, 18),
            MyColor: "white",
            Opponent: "Carol",
            MyRating: 1500,
            OppRating: 1480,
            Result: "win",
            Event: "  Club Open  ",
            Sans: ["e4", "e5"]);

        await repository.AddManualGameAsync(game);

        var keys = await repository.LoadTournamentKeysAsync();
        Assert.Equal("otb:Club Open", keys[game.Id]);
    }

    [Fact]
    public async Task Tournament_keys_return_the_url_of_a_chess_com_tournament_game()
    {
        using var db = new TempDatabase();
        await db.Migrations.MigrateAsync();
        await db.ExecuteAsync(
            """
            INSERT INTO games(id, date, time, color, result, timeClass, tournament) VALUES
              ('cc-game', '2026-05-01', '10:00:00', 'white', 'win', 'blitz', 'https://www.chess.com/tournament/live/12345');
            """);

        var keys = await new TournamentRepository(db.Connections).LoadTournamentKeysAsync();

        Assert.Equal("https://www.chess.com/tournament/live/12345", keys["cc-game"]);
    }

    [Fact]
    public async Task Tournament_keys_leave_out_normal_games()
    {
        using var db = new TempDatabase();
        await db.Migrations.MigrateAsync();
        await db.ExecuteAsync(
            """
            INSERT INTO games(id, date, time, color, result, timeClass, tournament) VALUES
              ('normal', '2026-05-01', '10:00:00', 'white', 'win', 'blitz', NULL);
            """);

        var keys = await new TournamentRepository(db.Connections).LoadTournamentKeysAsync();

        Assert.Empty(keys);
    }

    [Fact]
    public async Task Games_tournament_column_exists_and_is_nullable()
    {
        using var db = new TempDatabase();
        await db.Migrations.MigrateAsync();

        Assert.Equal(1, await db.ScalarAsync<long>(
            "SELECT COUNT(*) FROM pragma_table_info('games') WHERE name = 'tournament' AND \"notnull\" = 0"));
    }

    private static ManualGame MakeGame(string[] Sans) => new(
        Date: new DateOnly(2026, 10, 18),
        MyColor: "white",
        Opponent: "Carol",
        MyRating: 1500,
        OppRating: 1480,
        Result: "win",
        Event: "Club Open",
        Sans: Sans);
}
