using ChessCoach.Core.Abstractions;

namespace ChessCoach.Core.Settings;

/// <summary>One of the app's colour themes. The colours themselves live in the UI project, keyed by <paramref name="Id"/>.</summary>
public sealed record ThemeOption(string Id, string Name, string Description, bool Dark);

public static class Themes
{
    public const string Walnut = "walnut";
    public const string Evergreen = "evergreen";
    public const string Birch = "birch";

    public static IReadOnlyList<ThemeOption> All { get; } =
    [
        new(Walnut, "Walnut Study", "Dark wood and warm brass.", Dark: true),
        new(Evergreen, "Evergreen Night", "Deep forest greens after dark.", Dark: true),
        new(Birch, "Birch & Moss", "Light birch with moss green.", Dark: false),
    ];

    public static ThemeOption Default => All[0];

    /// <summary>The option for <paramref name="id"/>, or the default when the id is unknown or missing.</summary>
    public static ThemeOption Find(string? id) => All.FirstOrDefault(t => t.Id == id) ?? Default;
}

/// <summary>Loads and saves the chosen theme. An unknown or missing saved value means the default theme.</summary>
public sealed class ThemeSettingsService(IAppSettingsStore settings)
{
    public const string SettingKey = "theme";

    public async Task<ThemeOption> GetAsync(CancellationToken cancellationToken = default) =>
        Themes.Find(await settings.GetAsync(SettingKey, cancellationToken));

    /// <summary>Saves the theme and returns the option that was saved (the default when <paramref name="id"/> is unknown).</summary>
    public async Task<ThemeOption> SetAsync(string id, CancellationToken cancellationToken = default)
    {
        var theme = Themes.Find(id);
        await settings.SetAsync(SettingKey, theme.Id, cancellationToken);
        return theme;
    }
}
