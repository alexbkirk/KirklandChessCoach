using ChessCoach.Core.Setup;
using ChessCoach.Infrastructure;
using ChessCoach.Infrastructure.Data;
using ChessCoach.Infrastructure.Setup;
using Microsoft.Extensions.Options;

namespace ChessCoach.Infrastructure.Tests;

public sealed class SetupServiceTests : IDisposable
{
    private readonly string _dataDirectory = Path.Combine(Path.GetTempPath(), "chesscoach-setup-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dataDirectory)) Directory.Delete(_dataDirectory, recursive: true);
    }

    private SetupService Service(TempDatabase db) =>
        new(new AppSettingsStore(db.Connections), new GameRepository(db.Connections), db.Connections,
            Options.Create(new ChessCoachOptions { DataDirectory = _dataDirectory }));

    private void AddAllEngines()
    {
        foreach (var engine in EngineCatalog.All)
        {
            var path = Path.Combine(_dataDirectory, engine.RelativeMainPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "engine");
        }
    }

    [Fact]
    public async Task A_new_install_needs_everything_and_saving_a_username_is_remembered()
    {
        using var db = new TempDatabase();
        await db.Migrations.MigrateAsync();
        var service = Service(db);

        var before = await service.GetStatusAsync();
        Assert.False(before.UsernameChosen);
        Assert.Equal(EngineCatalog.All.Count, before.MissingEngines.Count);
        Assert.Equal(0, before.GameCount);
        Assert.Null(before.ImportFinished);
        Assert.False(before.Complete);

        Assert.Null(await service.SaveUsernameAsync("  xkirke  "));
        Assert.Equal("xkirke", (await service.GetStatusAsync()).Username);
    }

    [Fact]
    public async Task A_bad_username_is_refused_with_a_reason_and_not_saved()
    {
        using var db = new TempDatabase();
        await db.Migrations.MigrateAsync();
        var service = Service(db);

        var reason = await service.SaveUsernameAsync("no spaces allowed");

        Assert.NotNull(reason);
        Assert.Null((await service.GetStatusAsync()).Username);
    }

    [Fact]
    public async Task An_engine_counts_as_present_only_when_its_main_file_is_in_the_data_folder()
    {
        using var db = new TempDatabase();
        await db.Migrations.MigrateAsync();
        var maia = Path.Combine(_dataDirectory, "engine", "maia");
        Directory.CreateDirectory(maia);
        File.WriteAllText(Path.Combine(maia, "maia-1100.pb.gz"), "weights");

        var status = await Service(db).GetStatusAsync();

        Assert.DoesNotContain(status.MissingEngines, e => e.FolderName == "maia");
        Assert.Equal(EngineCatalog.All.Count - 1, status.MissingEngines.Count);
    }

    [Fact]
    public async Task A_paused_import_is_not_done_even_with_games_in_the_database()
    {
        using var db = new TempDatabase();
        await db.Migrations.MigrateAsync();
        AddAllEngines();
        var service = Service(db);
        await service.SaveUsernameAsync("xkirke");
        await service.MarkImportStartedAsync();
        // the import fetched two games, then was paused before analysing them
        await db.ExecuteAsync("INSERT INTO games(id, date) VALUES ('g1', '2026-01-01'), ('g2', '2026-01-02');");

        var status = await service.GetStatusAsync();

        Assert.Equal(2, status.GameCount);
        Assert.Equal(2, status.UnanalysedGames);
        Assert.False(status.ImportDone);
        Assert.False(status.Complete);

        await service.MarkImportFinishedAsync();
        Assert.True((await service.GetStatusAsync()).Complete);
    }

    [Fact]
    public async Task A_finished_import_with_no_rapid_games_still_completes_setup()
    {
        using var db = new TempDatabase();
        await db.Migrations.MigrateAsync();
        AddAllEngines();
        var service = Service(db);
        await service.SaveUsernameAsync("blitz_only");
        await service.MarkImportStartedAsync();
        await service.MarkImportFinishedAsync();

        var status = await service.GetStatusAsync();

        Assert.Equal(0, status.GameCount);
        Assert.True(status.Complete);
    }

    [Fact]
    public async Task A_database_from_before_the_setup_page_counts_as_imported_once_every_game_is_analysed()
    {
        using var db = new TempDatabase();
        await db.Migrations.MigrateAsync();
        AddAllEngines();
        var service = Service(db);
        await service.SaveUsernameAsync("xkirke");
        await db.ExecuteAsync("INSERT INTO games(id, date) VALUES ('g1', '2026-01-01');");

        Assert.False((await service.GetStatusAsync()).ImportDone);   // not analysed yet

        await db.ExecuteAsync("INSERT INTO game_analysis(game_id) VALUES ('g1');");

        var status = await service.GetStatusAsync();
        Assert.Null(status.ImportFinished);
        Assert.True(status.Complete);
    }
}
