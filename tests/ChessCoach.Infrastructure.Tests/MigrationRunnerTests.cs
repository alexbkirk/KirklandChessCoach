using ChessCoach.Infrastructure.Data;

namespace ChessCoach.Infrastructure.Tests;

public sealed class MigrationRunnerTests
{
    private static readonly string[] ExpectedTables =
    [
        "app_settings", "blunder_notes", "blunder_tags", "coach_notes", "drill_days", "drill_slots", "game_analysis",
        "games", "moves", "puzzle_attempts", "puzzle_progress", "schema_version",
    ];

    [Fact]
    public async Task Migrating_an_empty_database_creates_every_table_and_enables_wal()
    {
        using var db = new TempDatabase();

        await db.Migrations.MigrateAsync();

        var tables = (await db.ScalarAsync<string>(
            "SELECT group_concat(name, ',') FROM (SELECT name FROM sqlite_master WHERE type='table' ORDER BY name)")).Split(',');
        Assert.Equal(ExpectedTables, tables);
        Assert.Equal("wal", await db.ScalarAsync<string>("PRAGMA journal_mode"));
    }

    [Fact]
    public async Task Migrating_twice_applies_each_migration_once()
    {
        using var db = new TempDatabase();

        await db.Migrations.MigrateAsync();
        await db.Migrations.MigrateAsync();

        var expected = MigrationRunner.LoadMigrations().Count;
        Assert.Equal(expected, await db.ScalarAsync<int>("SELECT COUNT(*) FROM schema_version"));
        Assert.Equal(expected, await db.ScalarAsync<int>("SELECT MAX(version) FROM schema_version"));
    }

    [Fact]
    public async Task Migrating_a_database_created_by_the_python_pipeline_keeps_its_rows()
    {
        using var db = new TempDatabase();
        // The schema exactly as the Python scripts created it, before the app existed.
        await db.ExecuteAsync(
            """
            CREATE TABLE games (id, url, date, time, color, result, ending, timeControl, timeClass, myRating, oppRating, opponent, eco, opening, family, plies, moves, pgn, primary key(id));
            CREATE TABLE moves(game_id text, ply int, color text, is_mine int, san text, uci text, fen_before text, best_uci text, best_san text, eval_before int, eval_after int, cp_loss int, class text, is_last int default 0, clk real, primary key(game_id, ply));
            CREATE TABLE game_analysis(game_id text primary key, depth int, my_acpl real, n_my_moves int, inaccuracies int, mistakes int, blunders int);
            CREATE TABLE blunder_tags(game_id text, ply int, tags text, primary key(game_id, ply));
            CREATE TABLE coach_notes(game_id text, ply int, note text, primary key(game_id, ply));
            INSERT INTO games(id, date, color, result) VALUES ('g1', '2026-10-16', 'white', 'win');
            INSERT INTO moves(game_id, ply, san, cp_loss, class) VALUES ('g1', 1, 'e4', 0, 'best');
            """);

        await db.Migrations.MigrateAsync();

        Assert.Equal(1, await db.ScalarAsync<int>("SELECT COUNT(*) FROM games"));
        Assert.Equal("e4", await db.ScalarAsync<string>("SELECT san FROM moves WHERE game_id='g1' AND ply=1"));
        Assert.Equal(MigrationRunner.LoadMigrations().Count, await db.ScalarAsync<int>("SELECT COUNT(*) FROM schema_version"));
    }

    [Fact]
    public void Migration_files_are_numbered_without_gaps_or_duplicates()
    {
        var versions = MigrationRunner.LoadMigrations().Select(m => m.Version).ToArray();

        Assert.NotEmpty(versions);
        Assert.Equal(Enumerable.Range(1, versions.Length), versions);
    }
}
