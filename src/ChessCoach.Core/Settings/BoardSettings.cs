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

    /// <summary>
    /// Keeps a size within the allowed range and on a step, so a hand-edited setting cannot make a board unusable. A size halfway
    /// between two steps goes to the larger one.
    /// </summary>
    public static int Clamp(int size) => Clamp((double)size);

    /// <summary>Safe for any number, however large or small; text that is not a number is not handled here (see <see cref="Parse"/>).</summary>
    private static int Clamp(double size)
    {
        if (double.IsNaN(size)) return Default;
        var onStep = Math.Round(Math.Clamp(size, Min, Max) / Step, MidpointRounding.AwayFromZero) * Step;
        return (int)Math.Clamp(onStep, Min, Max);
    }

    /// <summary>
    /// The size for the saved text: a whole number (a sign is allowed) is clamped to the range, however large or small; anything
    /// else, or nothing, means the default.
    /// </summary>
    public static int Parse(string? saved) =>
        double.TryParse(saved, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var size) ? Clamp(size) : Default;

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
