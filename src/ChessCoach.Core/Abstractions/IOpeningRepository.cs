using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Abstractions;

public interface IOpeningRepository
{
    /// <summary>Every game with its blunder count, first slip and evaluation after move 10 (from your side).</summary>
    Task<IReadOnlyList<OpeningGame>> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Your games in the scope, and their first 15 moves (both sides), for the variation page.</summary>
    Task<(IReadOnlyList<VariationGame> Games, IReadOnlyList<VariationPly> Plies)> LoadVariationAsync(OpeningScope scope, CancellationToken cancellationToken = default);

    /// <summary>Practice history per position key, for the given keys.</summary>
    Task<IReadOnlyDictionary<string, PracticeTally>> LoadPracticeAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default);

    Task RecordPracticeAsync(string key, string playedUci, bool correct, DateTimeOffset atUtc, CancellationToken cancellationToken = default);
}
