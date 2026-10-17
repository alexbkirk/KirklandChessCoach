using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Abstractions;

public interface IPuzzleRepository
{
    /// <summary>All coached blunders as puzzles, costliest first.</summary>
    Task<IReadOnlyList<Puzzle>> LoadPoolAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, PuzzleProgress>> LoadProgressAsync(CancellationToken cancellationToken = default);

    /// <summary>Stores the updated progress and appends the attempt to the history, in one transaction.</summary>
    Task RecordAttemptAsync(PuzzleProgress progress, string playedUci, bool correct, bool gaveUp, bool hintUsed, DateTimeOffset atUtc, CancellationToken cancellationToken = default);
}
