namespace ChessCoach.UI.Components;

/// <summary>One move tried in a <see cref="PracticePosition"/> (or the player giving up).</summary>
/// <param name="Uci">The move played, or null when the player gave up.</param>
/// <param name="Round">1 for the first time through the position, 2 after Try again, and so on.</param>
/// <param name="Number">1 for the first move tried in this round.</param>
/// <param name="HintUsed">A hint (asked for, or given after two strikes) was showing when the move was made.</param>
public sealed record PracticeAttempt(string? Uci, string? San, bool Correct, bool GaveUp, int Round, int Number, bool HintUsed)
{
    /// <summary>The first move of the first round: the one that shows whether the position is known.</summary>
    public bool IsFirst => Round == 1 && Number == 1;
}
