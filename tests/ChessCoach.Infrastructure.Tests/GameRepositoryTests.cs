using ChessCoach.Infrastructure.Data;

namespace ChessCoach.Infrastructure.Tests;

public sealed class GameRepositoryTests
{
    [Fact]
    public async Task Summary_of_an_empty_database_has_zero_counts_and_no_dates()
    {
        using var db = new TempDatabase();
        await db.Migrations.MigrateAsync();

        var summary = await new GameRepository(db.Connections).GetSummaryAsync();

        Assert.Equal(0, summary.GameCount);
        Assert.Equal(0, summary.MoveCount);
        Assert.Null(summary.FirstGameDate);
        Assert.Null(summary.LastGameDate);
    }

    [Fact]
    public async Task Summary_counts_games_and_moves_and_reports_the_date_range()
    {
        using var db = new TempDatabase();
        await db.Migrations.MigrateAsync();
        await db.ExecuteAsync(
            """
            INSERT INTO games(id, date) VALUES ('a', '2025-01-02'), ('b', '2026-10-16'), ('c', '2025-06-30');
            INSERT INTO moves(game_id, ply) VALUES ('a', 1), ('a', 2), ('b', 1);
            """);

        var summary = await new GameRepository(db.Connections).GetSummaryAsync();

        Assert.Equal(3, summary.GameCount);
        Assert.Equal(3, summary.MoveCount);
        Assert.Equal("2025-01-02", summary.FirstGameDate);
        Assert.Equal("2026-10-16", summary.LastGameDate);
    }
}
