using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Settings;

namespace ChessCoach.Core.Tests;

public sealed class BoardSettingsTests
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

    [Fact]
    public void Every_size_the_buttons_can_reach_is_on_a_step_inside_the_range()
    {
        var size = BoardSizes.Default;
        for (var i = 0; i < 30; i++) size = BoardSizes.Next(size, larger: true);
        Assert.Equal(880, size);
        for (var i = 0; i < 30; i++) size = BoardSizes.Next(size, larger: false);
        Assert.Equal(320, size);
    }

    [Theory]
    [InlineData(520, 520)]
    [InlineData(0, 320)]
    [InlineData(-500, 320)]
    [InlineData(100, 320)]
    [InlineData(9999, 880)]
    [InlineData(900, 880)]
    [InlineData(530, 520)]
    [InlineData(545, 560)]
    [InlineData(540, 560)]
    [InlineData(580, 600)]
    [InlineData(int.MaxValue, 880)]
    [InlineData(int.MinValue, 320)]
    public void A_size_is_kept_inside_the_range_and_on_a_step(int size, int expected)
    {
        Assert.Equal(expected, BoardSizes.Clamp(size));
    }

    [Theory]
    [InlineData("600", 600)]
    [InlineData("9999", 880)]
    [InlineData("10", 320)]
    [InlineData("-1", 320)]
    [InlineData("99999999999", 880)]
    [InlineData("99999999999999999999999", 880)]
    [InlineData("-99999999999999999999999", 320)]
    [InlineData("+700", 720)]
    [InlineData("big", 520)]
    [InlineData("", 520)]
    [InlineData(null, 520)]
    [InlineData("5.5", 520)]
    public void A_saved_size_is_read_safely(string? saved, int expected)
    {
        Assert.Equal(expected, BoardSizes.Parse(saved));
    }

    [Fact]
    public async Task The_size_is_saved_and_read_back()
    {
        var store = new MemoryStore();
        var service = new BoardSettingsService(store);

        var kept = await service.SetSizeAsync(640);

        Assert.Equal(640, kept);
        Assert.Equal("640", store.Values[BoardSettingsService.SettingKey]);
        Assert.Equal(640, await new BoardSettingsService(store).GetSizeAsync());
    }

    [Fact]
    public async Task A_size_outside_the_range_is_saved_as_the_nearest_allowed_one()
    {
        var store = new MemoryStore();

        var kept = await new BoardSettingsService(store).SetSizeAsync(5000);

        Assert.Equal(880, kept);
        Assert.Equal("880", store.Values[BoardSettingsService.SettingKey]);
    }

    [Fact]
    public async Task With_nothing_saved_the_default_size_is_used()
    {
        Assert.Equal(BoardSizes.Default, await new BoardSettingsService(new MemoryStore()).GetSizeAsync());
    }
}

public sealed class BoardViewTests
{
    private sealed class CountingStore(bool failReads = false, bool failWrites = false) : IAppSettingsStore
    {
        public Dictionary<string, string> Values { get; } = [];

        public int Writes { get; private set; }

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
            failReads ? Task.FromException<string?>(new IOException("read")) : Task.FromResult(Values.TryGetValue(key, out var v) ? v : null);

        public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            if (failWrites) return Task.FromException(new IOException("write"));
            Writes++;
            Values[key] = value;
            return Task.CompletedTask;
        }
    }

    private static BoardView View(CountingStore? store = null, int graceMilliseconds = 30) =>
        new(new BoardSettingsService(store ?? new CountingStore())) { Grace = TimeSpan.FromMilliseconds(graceMilliseconds) };

    [Fact]
    public async Task The_saved_size_is_loaded_and_a_failed_load_means_the_default()
    {
        var saved = new CountingStore();
        saved.Values[BoardSettingsService.SettingKey] = "680";
        var view = View(saved);
        await view.LoadAsync();
        Assert.Equal(680, view.Size);

        var broken = View(new CountingStore(failReads: true));
        await broken.LoadAsync();
        Assert.Equal(BoardSizes.Default, broken.Size);
    }

    [Fact]
    public async Task Changing_the_size_steps_saves_and_tells_the_layout()
    {
        var store = new CountingStore();
        var view = View(store);
        var changes = 0;
        view.Changed += () => changes++;

        await view.ChangeSizeAsync(larger: true);

        Assert.Equal(560, view.Size);
        Assert.Equal("560", store.Values[BoardSettingsService.SettingKey]);
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task At_the_limits_a_press_changes_nothing_and_saves_nothing()
    {
        var store = new CountingStore();
        var view = View(store);
        await view.SetSizeAsync(BoardSizes.Max);
        var writes = store.Writes;
        var changes = 0;
        view.Changed += () => changes++;

        await view.ChangeSizeAsync(larger: true);

        Assert.Equal(BoardSizes.Max, view.Size);
        Assert.False(view.CanGrow);
        Assert.Equal(writes, store.Writes);
        Assert.Equal(0, changes);

        await view.SetSizeAsync(BoardSizes.Min);
        writes = store.Writes;
        await view.ChangeSizeAsync(larger: false);
        Assert.Equal(BoardSizes.Min, view.Size);
        Assert.False(view.CanShrink);
        Assert.Equal(writes, store.Writes);
    }

    [Fact]
    public async Task A_failed_save_still_changes_the_board_for_now()
    {
        var view = View(new CountingStore(failWrites: true));

        await view.SetSizeAsync(640);

        Assert.Equal(640, view.Size);
    }

    [Fact]
    public void Focus_mode_toggles_and_can_be_left()
    {
        var view = View();

        view.ToggleFocus();
        Assert.True(view.FocusMode);
        view.ExitFocus();
        Assert.False(view.FocusMode);
        view.ExitFocus();
        Assert.False(view.FocusMode);
    }

    [Fact]
    public void Boards_are_counted_as_they_come_and_go_and_never_below_zero()
    {
        var view = View();

        view.BoardAppeared();
        view.BoardAppeared();
        view.BoardGone();
        Assert.Equal(1, view.BoardsOnPage);
        view.BoardGone();
        view.BoardGone();
        Assert.Equal(0, view.BoardsOnPage);
    }

    [Fact]
    public async Task Focus_mode_ends_when_the_last_board_leaves_and_none_comes_back()
    {
        var view = View();
        view.BoardAppeared();
        view.ToggleFocus();

        view.BoardGone();
        await view.Settled;

        Assert.False(view.FocusMode);
    }

    [Fact]
    public async Task Focus_mode_stays_on_when_the_next_pages_board_appears_just_after_the_last_one_goes()
    {
        var view = View(graceMilliseconds: 80);
        view.BoardAppeared();
        view.ToggleFocus();

        view.BoardGone();
        view.BoardAppeared();
        await view.Settled;

        Assert.True(view.FocusMode);
        Assert.Equal(1, view.BoardsOnPage);
    }

    [Fact]
    public async Task Leaving_one_of_two_boards_keeps_focus_mode()
    {
        var view = View();
        view.BoardAppeared();
        view.BoardAppeared();
        view.ToggleFocus();

        view.BoardGone();
        await view.Settled;

        Assert.True(view.FocusMode);
    }
}
