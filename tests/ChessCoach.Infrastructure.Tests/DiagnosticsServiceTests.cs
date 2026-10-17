using ChessCoach.Core.Domain;
using ChessCoach.Infrastructure.Data;
using ChessCoach.Infrastructure.Diagnostics;
using Microsoft.Extensions.Options;

namespace ChessCoach.Infrastructure.Tests;

public sealed class DiagnosticsServiceTests
{
    private static DiagnosticsService CreateService(TempDatabase db) =>
        new(Microsoft.Extensions.Options.Options.Create(db.Options), db.Connections, new GameRepository(db.Connections));

    [Fact]
    public async Task A_missing_database_and_engines_are_reported_as_errors()
    {
        using var db = new TempDatabase();

        var checks = await CreateService(db).RunAsync();

        Assert.Equal(DiagnosticStatus.Ok, Status(checks, "Data folder"));
        Assert.Equal(DiagnosticStatus.Error, Status(checks, "Database file"));
        Assert.Equal(DiagnosticStatus.Error, Status(checks, "Stockfish"));
        Assert.Equal(DiagnosticStatus.Warning, Status(checks, "Analysis scripts"));
    }

    [Fact]
    public async Task A_migrated_database_reports_its_games_wal_mode_and_schema_version()
    {
        using var db = new TempDatabase();
        await db.Migrations.MigrateAsync();
        await db.ExecuteAsync("INSERT INTO games(id, date) VALUES ('a', '2026-01-01'), ('b', '2026-02-02');");

        var checks = await CreateService(db).RunAsync();

        Assert.Equal(DiagnosticStatus.Ok, Status(checks, "Database file"));
        Assert.Contains("2 games", checks.Single(c => c.Name == "Games").Detail, StringComparison.Ordinal);
        Assert.Equal(DiagnosticStatus.Ok, Status(checks, "Journal mode"));
        Assert.Equal(MigrationRunner.LoadMigrations().Count.ToString(System.Globalization.CultureInfo.InvariantCulture), checks.Single(c => c.Name == "Schema version").Detail);
    }

    [Fact]
    public async Task Engines_that_exist_on_disk_are_reported_ok()
    {
        using var db = new TempDatabase();
        Directory.CreateDirectory(Path.GetDirectoryName(db.Options.StockfishPath)!);
        await File.WriteAllTextAsync(db.Options.StockfishPath, "stub");

        var checks = await CreateService(db).RunAsync();

        Assert.Equal(DiagnosticStatus.Ok, Status(checks, "Stockfish"));
        Assert.Equal(DiagnosticStatus.Error, Status(checks, "lc0"));
    }

    private static DiagnosticStatus Status(IReadOnlyList<DiagnosticCheck> checks, string name) => checks.Single(c => c.Name == name).Status;
}
