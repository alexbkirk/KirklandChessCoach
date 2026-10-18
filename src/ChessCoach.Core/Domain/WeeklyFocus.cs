namespace ChessCoach.Core.Domain;

/// <summary>A game used for the focus.</summary>
public sealed record FocusGame(string Id, DateOnly Date, string Opponent, string Url);

/// <summary>
/// One of your blunders (not a final move), with its explanation when it has one.
/// <paramref name="Side"/> and <paramref name="Motif"/> come from the note: what the move allowed or missed, and the tactic.
/// </summary>
/// <param name="Swing">Winning chances the move cost (0 to 100), from the note; null when there is no note.</param>
public sealed record FocusBlunder(
    string GameId,
    int Ply,
    string San,
    string Fen,
    string Color,
    string? BestUci,
    string? BestSan,
    string? Side,
    string? Motif,
    string? Headline,
    double? Swing);

/// <summary>One kind of mistake and how often it happened in the window.</summary>
public sealed record LeakCount(string Key, string Label, int Count);

/// <summary>A recent blunder of the focus kind: what happened, and the position to practise.</summary>
/// <param name="Fen">The position before your move; you play <paramref name="Color"/>.</param>
public sealed record FocusExample(
    DateOnly Date,
    string Opponent,
    int Move,
    string Url,
    string PlayedSan,
    string? Headline,
    string Fen,
    string Color,
    string? BestUci,
    string? BestSan);

/// <summary>
/// The focus: your most common kind of blunder in a recent stretch of games, how it compares with the rest, and examples.
/// <paramref name="Focus"/> is null when no blunder in the window has an explanation yet.
/// </summary>
/// <param name="Window">"your last 50 games" or "the last 30 days".</param>
/// <param name="BlunderCount">Blunders in the window that cost at least <c>MinSwing</c> points of winning chances.</param>
/// <param name="OtherCount">Of those, the ones with no single named tactic behind them.</param>
/// <param name="UnexplainedCount">Blunders in the window not explained yet (the next import explains them).</param>
/// <param name="OverallTop">Your most common kind across all your games, or null.</param>
public sealed record WeeklyFocus(
    string Window,
    int GameCount,
    DateOnly? From,
    DateOnly? To,
    int BlunderCount,
    int OtherCount,
    int UnexplainedCount,
    LeakInfo? Focus,
    IReadOnlyList<LeakCount> Top,
    LeakInfo? OverallTop,
    IReadOnlyList<FocusExample> Examples);

/// <summary>A kind of mistake: its name, what it means, and one habit that guards against it.</summary>
public sealed record LeakInfo(string Key, string Label, string Description, string TryThis);
