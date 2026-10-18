using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Settings;

namespace ChessCoach.Core.Tests;

public sealed class TimeClassTests
{
    private sealed class MemoryStore : IAppSettingsStore
    {
        public Dictionary<string, string> Values { get; } = [];

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(Values.TryGetValue(key, out var value) ? value : null);

        public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            Values[key] = value;
            return Task.CompletedTask;
        }
    }

    [Theory]
    [InlineData(null, "rapid")]
    [InlineData("", "rapid")]
    [InlineData("rapid", "rapid")]
    [InlineData("blitz", "rapid,blitz")]
    [InlineData("bullet,blitz", "rapid,blitz,bullet")]
    [InlineData("blitz, blitz ,BLITZ", "rapid,blitz")]
    [InlineData("daily,chess960,x", "rapid")]
    [InlineData("rapid,blitz,bullet", "rapid,blitz,bullet")]
    public void The_saved_list_is_read_safely_and_always_includes_rapid(string? saved, string expected)
    {
        Assert.Equal(expected, string.Join(',', TimeClasses.Parse(saved)));
    }

    [Fact]
    public async Task Turning_a_class_on_asks_the_next_import_to_search_every_month_until_one_finishes()
    {
        var store = new MemoryStore();
        var settings = new TimeClassSettings(store);
        Assert.False((await settings.GetImportPlanAsync()).Backfill);

        await settings.SetImportClassesAsync(["blitz"]);
        var plan = await settings.GetImportPlanAsync();
        Assert.Equal(["rapid", "blitz"], plan.Classes);
        Assert.True(plan.Backfill);

        await settings.ClearBackfillAsync();
        Assert.False((await settings.GetImportPlanAsync()).Backfill);
    }

    [Fact]
    public async Task Turning_a_class_off_or_saving_the_same_list_does_not_ask_for_a_backfill()
    {
        var store = new MemoryStore();
        var settings = new TimeClassSettings(store);
        await settings.SetImportClassesAsync(["blitz", "bullet"]);
        await settings.ClearBackfillAsync();

        await settings.SetImportClassesAsync(["blitz", "bullet"]);
        Assert.False((await settings.GetImportPlanAsync()).Backfill);

        await settings.SetImportClassesAsync(["blitz"]);
        Assert.False((await settings.GetImportPlanAsync()).Backfill);
    }

    [Fact]
    public async Task Turning_everything_but_rapid_off_again_cancels_a_pending_backfill()
    {
        var settings = new TimeClassSettings(new MemoryStore());
        await settings.SetImportClassesAsync(["blitz"]);
        Assert.True((await settings.GetImportPlanAsync()).Backfill);

        await settings.SetImportClassesAsync([]);

        Assert.False((await settings.GetImportPlanAsync()).Backfill);
    }

    [Fact]
    public async Task A_backfill_that_is_still_pending_stays_pending_when_another_class_is_added_or_removed()
    {
        var settings = new TimeClassSettings(new MemoryStore());
        await settings.SetImportClassesAsync(["blitz"]);

        await settings.SetImportClassesAsync(["blitz", "bullet"]);
        await settings.SetImportClassesAsync(["bullet"]);

        Assert.True((await settings.GetImportPlanAsync()).Backfill);
    }

    [Fact]
    public async Task The_view_is_rapid_until_chosen_and_only_imported_classes_or_all_can_be_viewed()
    {
        var store = new MemoryStore();
        var settings = new TimeClassSettings(store);
        Assert.Equal("rapid", await settings.GetViewAsync());

        await settings.SetViewAsync("blitz");
        Assert.Equal("rapid", await settings.GetViewAsync());   // blitz is not imported yet

        await settings.SetImportClassesAsync(["blitz"]);
        Assert.Equal("blitz", await settings.GetViewAsync());

        await settings.SetViewAsync("all");
        Assert.Equal("all", await settings.GetViewAsync());

        await settings.SetViewAsync("nonsense");
        Assert.Equal("rapid", await settings.GetViewAsync());
    }

    [Fact]
    public async Task A_saved_view_of_a_class_that_is_turned_off_falls_back_to_rapid()
    {
        var settings = new TimeClassSettings(new MemoryStore());
        await settings.SetImportClassesAsync(["bullet"]);
        await settings.SetViewAsync("bullet");

        await settings.SetImportClassesAsync([]);

        Assert.Equal("rapid", await settings.GetViewAsync());
    }

    [Fact]
    public async Task The_shared_view_filters_by_the_selection_and_tells_listeners()
    {
        var store = new MemoryStore();
        var settings = new TimeClassSettings(store);
        var view = new TimeClassView(settings);
        var changes = 0;
        view.Changed += () => changes++;

        await view.LoadAsync();
        Assert.Equal("rapid", view.Current);

        await view.SetImportedAsync(["blitz"]);
        await view.SelectAsync("blitz");
        Assert.Equal("blitz", view.Current);
        Assert.Equal("blitz", store.Values[TimeClassSettings.ViewKey]);

        await view.SelectAsync("all");
        Assert.Null(view.Current);
        Assert.Equal("all", view.Selection);
        Assert.True(changes >= 4);
    }

    [Fact]
    public async Task Turning_off_the_class_being_viewed_goes_back_to_rapid()
    {
        var view = new TimeClassView(new TimeClassSettings(new MemoryStore()));
        await view.SetImportedAsync(["blitz"]);
        await view.SelectAsync("blitz");

        await view.SetImportedAsync([]);

        Assert.Equal("rapid", view.Current);
        Assert.Equal(["rapid"], view.Imported);
    }

    [Fact]
    public async Task Selecting_a_class_that_is_not_imported_shows_rapid()
    {
        var view = new TimeClassView(new TimeClassSettings(new MemoryStore()));
        await view.LoadAsync();

        await view.SelectAsync("bullet");

        Assert.Equal("rapid", view.Current);
    }

    [Fact]
    public async Task The_view_keeps_working_when_the_settings_cannot_be_read()
    {
        var view = new TimeClassView(new TimeClassSettings(new ThrowingStore()));

        await view.LoadAsync();

        Assert.Equal("rapid", view.Current);
        Assert.Equal(["rapid"], view.Imported);
    }

    private sealed class ThrowingStore : IAppSettingsStore
    {
        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) => throw new IOException("db");

        public Task SetAsync(string key, string value, CancellationToken cancellationToken = default) => throw new IOException("db");
    }

    [Fact]
    public async Task A_finishing_import_clears_the_backfill_only_when_the_classes_are_still_the_ones_it_searched()
    {
        var settings = new TimeClassSettings(new MemoryStore());
        await settings.SetImportClassesAsync(["blitz"]);
        var searched = (await settings.GetImportPlanAsync()).Classes;   // the import starts with rapid and blitz

        await settings.SetImportClassesAsync(["blitz", "bullet"]);      // bullet is turned on while it runs
        await settings.ClearBackfillAsync(searched);                    // the import finishes

        Assert.True((await settings.GetImportPlanAsync()).Backfill);    // the next import still searches for bullet

        await settings.ClearBackfillAsync((await settings.GetImportPlanAsync()).Classes);
        Assert.False((await settings.GetImportPlanAsync()).Backfill);
    }

    [Fact]
    public async Task A_saved_view_of_all_means_rapid_while_only_rapid_is_imported()
    {
        var settings = new TimeClassSettings(new MemoryStore());
        await settings.SetViewAsync("all");

        Assert.Equal("rapid", await settings.GetViewAsync());

        await settings.SetImportClassesAsync(["blitz"]);
        Assert.Equal("all", await settings.GetViewAsync());
    }

    [Fact]
    public async Task A_failed_save_of_the_imported_classes_keeps_the_previous_ones_and_does_not_throw()
    {
        var store = new FailingWriteStore();
        var view = new TimeClassView(new TimeClassSettings(store));
        await view.LoadAsync();

        await view.SetImportedAsync(["blitz"]);

        Assert.Equal(["rapid"], view.Imported);
    }

    private sealed class FailingWriteStore : IAppSettingsStore
    {
        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

        public Task SetAsync(string key, string value, CancellationToken cancellationToken = default) => throw new IOException("write");
    }
}
