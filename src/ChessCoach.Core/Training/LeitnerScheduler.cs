using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Training;

/// <summary>
/// Leitner boxes: a correct answer moves a puzzle up one box and schedules it for that box's interval;
/// a miss sends it back to box 0 and due again after a short retry delay.
/// </summary>
public static class LeitnerScheduler
{
    public const int MaxBox = 6;

    /// <summary>Days until the next review, indexed by box (box 0 is never a successful answer).</summary>
    public static readonly int[] IntervalDays = [0, 1, 3, 7, 14, 30, 60];

    public static readonly TimeSpan MissRetryDelay = TimeSpan.FromMinutes(5);

    public static PuzzleProgress Apply(PuzzleProgress current, bool correct, DateTimeOffset now)
    {
        var box = correct ? Math.Min(current.Box + 1, MaxBox) : 0;
        var due = correct ? now + TimeSpan.FromDays(IntervalDays[box]) : now + MissRetryDelay;
        return current with
        {
            Box = box,
            DueUtc = due,
            Tries = current.Tries + 1,
            Correct = current.Correct + (correct ? 1 : 0),
            LastSeenUtc = now,
        };
    }

    /// <summary>State for a puzzle that has never been answered: due now, in box 0.</summary>
    public static PuzzleProgress New(string positionId, DateTimeOffset now) =>
        new(positionId, 0, now, 0, 0, null);
}
