using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Training;

/// <summary>
/// Chooses the next set of puzzles. Order: due reviews first (lowest box, then most overdue), then never-seen
/// puzzles (costliest blunder first, as the pool is supplied), then reviews that are not yet due (soonest first).
/// </summary>
public static class PuzzleSet
{
    public const int DefaultSize = 5;

    public static IReadOnlyList<Puzzle> Pick(
        IReadOnlyList<Puzzle> pool,
        IReadOnlyDictionary<string, PuzzleProgress> progress,
        DateTimeOffset now,
        int size = DefaultSize)
    {
        var due = pool
            .Where(p => progress.TryGetValue(p.PositionId, out var s) && s.DueUtc <= now)
            .OrderBy(p => progress[p.PositionId].Box)
            .ThenBy(p => progress[p.PositionId].DueUtc)
            .ToList();
        var fresh = pool.Where(p => !progress.ContainsKey(p.PositionId)).ToList();
        var later = pool
            .Where(p => progress.TryGetValue(p.PositionId, out var s) && s.DueUtc > now)
            .OrderBy(p => progress[p.PositionId].DueUtc)
            .ToList();
        return due.Concat(fresh).Concat(later).Take(size).ToList();
    }
}
