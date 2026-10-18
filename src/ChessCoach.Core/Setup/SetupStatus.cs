using System.Text.RegularExpressions;

namespace ChessCoach.Core.Setup;

/// <summary>What the first-run setup still needs. <see cref="Complete"/> means the app can be used normally.</summary>
/// <param name="Username">The chess.com username whose games are imported, or null when not chosen yet.</param>
/// <param name="MissingEngines">Engines whose files are not in the data folder yet.</param>
/// <param name="GameCount">Games in the database.</param>
public sealed record SetupStatus(string? Username, IReadOnlyList<EngineComponent> MissingEngines, int GameCount)
{
    public bool UsernameChosen => !string.IsNullOrWhiteSpace(Username);

    public bool Complete => UsernameChosen && MissingEngines.Count == 0 && GameCount > 0;
}

public static partial class ChessComUsername
{
    /// <summary>chess.com usernames: 3 to 25 letters, digits, underscores or hyphens.</summary>
    [GeneratedRegex("^[A-Za-z0-9_-]{3,25}$")]
    private static partial Regex Pattern();

    public static bool IsValid(string? name) => name is not null && Pattern().IsMatch(name.Trim());
}
