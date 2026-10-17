namespace ChessCoach.Core.Domain;

/// <summary>A game used for the focus (any game in the history, or only those in the window).</summary>
public sealed record FocusGame(string Id, DateOnly Date, string Opponent, string Url);

/// <summary>A balanced, non-final-move blunder with its tags.</summary>
public sealed record FocusBlunder(string GameId, int Ply, IReadOnlyList<string> Tags);

/// <summary>One leak: how often it happened in the window, per game, and against your lifetime rate.</summary>
/// <param name="DeltaPercent">Percent above (+) or below (-) your lifetime rate; null when you have no lifetime examples.</param>
public sealed record TagStat(string Key, string Label, int Count, double PerGame, double? DeltaPercent);

public sealed record FocusExample(DateOnly Date, string Opponent, int Move, string Url);

/// <summary>The weekly focus: your biggest leak in the window, with advice and examples. Title is null when there is none.</summary>
public sealed record WeeklyFocus(
    string Window,
    int GameCount,
    string? TopKey,
    string? Title,
    string? Description,
    string? Advice,
    IReadOnlyList<TagStat> Stats,
    IReadOnlyList<FocusExample> Examples);
