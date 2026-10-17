using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Training;

/// <summary>Use-case layer for puzzles: picks sets, grades answers and updates the schedule.</summary>
public sealed class PuzzleService(IPuzzleRepository repository, IClock clock)
{
    public async Task<IReadOnlyList<Puzzle>> NextSetAsync(CancellationToken cancellationToken = default)
    {
        var pool = await repository.LoadPoolAsync(cancellationToken);
        var progress = await repository.LoadProgressAsync(cancellationToken);
        return PuzzleSet.Pick(pool, progress, clock.UtcNow);
    }

    /// <summary>Grades an answer and records it. <paramref name="playedUci"/> is ignored when <paramref name="gaveUp"/> is true.</summary>
    public async Task<bool> AnswerAsync(Puzzle puzzle, string? playedUci, bool gaveUp, bool hintUsed, CancellationToken cancellationToken = default)
    {
        var progress = await CurrentProgressAsync(puzzle.PositionId, cancellationToken);
        var now = clock.UtcNow;
        var correct = !gaveUp && playedUci is not null && PuzzleGrader.IsCorrect(playedUci, puzzle.Accept);
        var updated = LeitnerScheduler.Apply(progress, correct, now);
        await repository.RecordAttemptAsync(updated, playedUci ?? "", correct, gaveUp, hintUsed, now, cancellationToken);
        return correct;
    }

    private async Task<PuzzleProgress> CurrentProgressAsync(string positionId, CancellationToken cancellationToken)
    {
        var all = await repository.LoadProgressAsync(cancellationToken);
        return all.TryGetValue(positionId, out var existing) ? existing : LeitnerScheduler.New(positionId, clock.UtcNow);
    }
}
