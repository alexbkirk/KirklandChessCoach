using System.Globalization;
using ChessCoach.Core.Abstractions;

namespace ChessCoach.Core.Settings;

/// <summary>The sizes the main boards can be shown at, in pixels across.</summary>
public static class BoardSizes
{
    public const int Min = 320;
    public const int Max = 880;
    public const int Step = 40;
    public const int Default = 520;

    /// <summary>Keeps a size within the allowed range and on a step, so a hand-edited setting cannot make a board unusable.</summary>
    public static int Clamp(int size) => Math.Clamp((int)Math.Round((double)size / Step) * Step, Min, Max);

    /// <summary>The size to use for the saved text: the default when it is missing or not a whole number.</summary>
    public static int Parse(string? saved) =>
        int.TryParse(saved, NumberStyles.None, CultureInfo.InvariantCulture, out var size) ? Clamp(size) : Default;

    /// <summary>One step bigger or smaller, within the range.</summary>
    public static int Next(int size, bool larger) => Clamp(size + (larger ? Step : -Step));
}

/// <summary>Loads and saves the board size. A missing or unreadable saved value means the default size.</summary>
public sealed class BoardSettingsService(IAppSettingsStore settings)
{
    public const string SettingKey = "board_size";

    public async Task<int> GetSizeAsync(CancellationToken cancellationToken = default) =>
        BoardSizes.Parse(await settings.GetAsync(SettingKey, cancellationToken));

    /// <summary>Saves the size and returns the size that was kept (clamped to the allowed range).</summary>
    public async Task<int> SetSizeAsync(int size, CancellationToken cancellationToken = default)
    {
        var kept = BoardSizes.Clamp(size);
        await settings.SetAsync(SettingKey, kept.ToString(CultureInfo.InvariantCulture), cancellationToken);
        return kept;
    }
}
