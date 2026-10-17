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
            INSERT INTO games(id, date, time, color, result, myRating, oppRating, timeControl, family, ending, plies, opponent) VALUES
              ('late', '2026-03-01', '09:00:00', 'black', 'draw', 1010, 1000, '900+10', 'Caro Kann Defense', 'agreement', 80, 'Eve'),
              ('early', '2025-12-31', '23:00:00', 'white', 'win', 990, 1000, '600', 'Pirc Defense', 'checkmate', 40, 'Bob');
            """);

        var games = await new StatsRepository(db.Connections).LoadGamesAsync();

        Assert.Equal(["early", "late"], games.Select(g => g.Id));
        Assert.Equal(new DateOnly(2025, 12, 31), games[0].Date);
        Assert.Equal("win", games[0].Result);
        Assert.Equal(1010, games[1].MyRating);
        Assert.Equal("900+10", games[1].TimeControl);
        Assert.Equal(80, games[1].Plies);
    }
}
