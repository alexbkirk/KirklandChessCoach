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

    private static Task<TempDatabase> NewDatabaseAsync() => Task.FromResult(new TempDatabase());

    [Fact]
    public async Task A_new_install_needs_everything_and_saving_a_username_is_remembered()
    {
        using var db = await NewDatabaseAsync();
        await db.Migrations.MigrateAsync();
        var options = Options.Create(new ChessCoachOptions { DataDirectory = _dataDirectory });
        var service = new SetupService(new AppSettingsStore(db.Connections), new GameRepository(db.Connections), options);

        var before = await service.GetStatusAsync();
        Assert.False(before.UsernameChosen);
        Assert.Equal(EngineCatalog.All.Count, before.MissingEngines.Count);
        Assert.Equal(0, before.GameCount);
        Assert.False(before.Complete);

        Assert.Null(await service.SaveUsernameAsync("  xkirke  "));
        Assert.Equal("xkirke", (await service.GetStatusAsync()).Username);
    }

    [Fact]
    public async Task A_bad_username_is_refused_with_a_reason_and_not_saved()
    {
        using var db = await NewDatabaseAsync();
        await db.Migrations.MigrateAsync();
        var service = new SetupService(new AppSettingsStore(db.Connections), new GameRepository(db.Connections),
            Options.Create(new ChessCoachOptions { DataDirectory = _dataDirectory }));

        var reason = await service.SaveUsernameAsync("no spaces allowed");

        Assert.NotNull(reason);
        Assert.Null((await service.GetStatusAsync()).Username);
    }

    [Fact]
    public async Task An_engine_counts_as_present_only_when_its_main_file_is_in_the_data_folder()
    {
        using var db = await NewDatabaseAsync();
        await db.Migrations.MigrateAsync();
        var service = new SetupService(new AppSettingsStore(db.Connections), new GameRepository(db.Connections),
            Options.Create(new ChessCoachOptions { DataDirectory = _dataDirectory }));
        var maia = Path.Combine(_dataDirectory, "engine", "maia");
        Directory.CreateDirectory(maia);
        File.WriteAllText(Path.Combine(maia, "maia-1100.pb.gz"), "weights");

        var status = await service.GetStatusAsync();

        Assert.DoesNotContain(status.MissingEngines, e => e.FolderName == "maia");
        Assert.Equal(EngineCatalog.All.Count - 1, status.MissingEngines.Count);
    }
}
