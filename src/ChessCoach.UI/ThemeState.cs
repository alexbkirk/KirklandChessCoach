using ChessCoach.Core.Settings;

namespace ChessCoach.UI;

/// <summary>The theme in use. The layout listens to <see cref="Changed"/>; the Settings page calls <see cref="SetAsync"/>.</summary>
public sealed class ThemeState(ThemeSettingsService settings)
{
    public ThemeOption Current { get; private set; } = Themes.Default;

    public bool Loaded { get; private set; }

    public event Action? Changed;

    public async Task LoadAsync()
    {
        if (Loaded) return;
        try
        {
            Current = await settings.GetAsync();
        }
        catch (Exception)
        {
            Current = Themes.Default;   // any storage failure: a colour preference is not worth stopping the app for
        }

        Loaded = true;
        Changed?.Invoke();
    }

    public async Task SetAsync(string id)
    {
        Current = await settings.SetAsync(id);
        Changed?.Invoke();
    }
}
