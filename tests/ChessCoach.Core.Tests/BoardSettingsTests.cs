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
    public void A_size_is_kept_inside_the_range_and_on_a_step(int size, int expected)
    {
        Assert.Equal(expected, BoardSizes.Clamp(size));
    }

    [Theory]
    [InlineData("600", 600)]
    [InlineData("9999", 880)]
    [InlineData("10", 320)]
    [InlineData("-1", 520)]
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
