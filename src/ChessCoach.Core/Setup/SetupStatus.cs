using System.Text.RegularExpressions;

namespace ChessCoach.Core.Setup;

/// <summary>What the first-run setup still needs. <see cref="Complete"/> means the app can be used normally.</summary>
/// <param name="Username">The chess.com username whose games are imported, or null when not chosen yet.</param>
/// <param name="MissingEngines">Engines whose files are not in the data folder yet.</param>
/// <param name="GameCount">Games in the database.</param>
/// <param name="UnanalysedGames">Games the analysis has not reached yet.</param>
/// <param name="ImportFinished">
/// True once an import has run to the end, false while one has started but not finished (paused or failed), and null
/// when no import was ever run from the app (a database set up before the setup page existed).
/// </param>
public sealed record SetupStatus(string? Username, IReadOnlyList<EngineComponent> MissingEngines, int GameCount, int UnanalysedGames = 0, bool? ImportFinished = null)
{
    public bool UsernameChosen => !string.IsNullOrWhiteSpace(Username);

    /// <summary>
    /// The import has run to the end. A player with no rapid games still finishes; a paused import does not, even though
    /// some games are already in the database. Databases from before the setup page count as done once every game is analysed.
    /// </summary>
    public bool ImportDone => ImportFinished ?? (GameCount > 0 && UnanalysedGames == 0);

    public bool Complete => UsernameChosen && MissingEngines.Count == 0 && ImportDone;
}

public static partial class ChessComUsername
{
    /// <summary>chess.com usernames: 3 to 25 letters, digits, underscores or hyphens.</summary>
    [GeneratedRegex("^[A-Za-z0-9_-]{3,25}$")]
    private static partial Regex Pattern();

    public static bool IsValid(string? name) => name is not null && Pattern().IsMatch(name.Trim());
}
