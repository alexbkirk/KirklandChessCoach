using ChessCoach.Infrastructure.Data;

namespace ChessCoach.Infrastructure.Tests;

public sealed class AppSettingsStoreTests
{
    [Fact]
    public async Task A_setting_is_null_until_set_then_updates_in_place()
    {
        using var db = new TempDatabase();
        await db.Migrations.MigrateAsync();
        var store = new AppSettingsStore(db.Connections);

        Assert.Null(await store.GetAsync("theme"));
        await store.SetAsync("theme", "birch");
        await store.SetAsync("theme", "evergreen");
        await store.SetAsync("other", "x");

        Assert.Equal("evergreen", await store.GetAsync("theme"));
        Assert.Equal("x", await store.GetAsync("other"));
        Assert.Equal(2, await db.ScalarAsync<int>("SELECT COUNT(*) FROM app_settings"));
    }
}
