namespace ChessCoach.Core.Training;

public static class PuzzleGrader
{
    /// <summary>True when the played UCI move is one of the accepted answers (case-insensitive).</summary>
    public static bool IsCorrect(string playedUci, IReadOnlyCollection<string> accept) =>
        accept.Any(a => string.Equals(a, playedUci, StringComparison.OrdinalIgnoreCase));
}
