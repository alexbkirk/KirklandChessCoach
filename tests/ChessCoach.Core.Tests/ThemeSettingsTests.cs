using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Settings;

namespace ChessCoach.Core.Tests;

public sealed class ThemeSettingsTests
{
    private sealed class FakeSettings : IAppSettingsStore
    {
        public Dictionary<string, string> Values { get; } = [];

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(Values.TryGetValue(key, out var v) ? v : null);

        public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            Values[key] = value;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void There_are_three_distinct_themes_and_the_first_is_the_default()
    {
        Assert.Equal(["walnut", "evergreen", "birch"], Themes.All.Select(t => t.Id));
        Assert.Equal(Themes.All.Count, Themes.All.Select(t => t.Name).Distinct().Count());
        Assert.Equal(Themes.Walnut, Themes.Default.Id);
        Assert.False(Themes.Find("birch").Dark);
        Assert.True(Themes.Find("evergreen").Dark);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("neon")]
    public async Task A_missing_or_unknown_saved_theme_means_the_default(string? saved)
    {
        var settings = new FakeSettings();
        if (saved is not null) settings.Values[ThemeSettingsService.SettingKey] = saved;

        var theme = await new ThemeSettingsService(settings).GetAsync();

        Assert.Equal(Themes.Walnut, theme.Id);
    }

    [Fact]
    public async Task A_saved_theme_is_read_back()
    {
        var settings = new FakeSettings();
        var service = new ThemeSettingsService(settings);

        var saved = await service.SetAsync(Themes.Birch);

        Assert.Equal(Themes.Birch, saved.Id);
        Assert.Equal("birch", settings.Values["theme"]);
        Assert.Equal(Themes.Birch, (await service.GetAsync()).Id);
    }

    [Fact]
    public async Task Saving_an_unknown_theme_saves_the_default_instead()
    {
        var settings = new FakeSettings();

        var saved = await new ThemeSettingsService(settings).SetAsync("neon");

        Assert.Equal(Themes.Walnut, saved.Id);
        Assert.Equal("walnut", settings.Values["theme"]);
    }
}
