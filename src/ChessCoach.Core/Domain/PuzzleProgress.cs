namespace ChessCoach.Core.Domain;

/// <summary>Spaced-repetition state for one puzzle (Leitner boxes 0 to 6).</summary>
public sealed record PuzzleProgress(
    string PositionId,
    int Box,
    DateTimeOffset DueUtc,
    int Tries,
    int Correct,
    DateTimeOffset? LastSeenUtc);
