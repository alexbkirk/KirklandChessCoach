using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Abstractions;

public interface IOpeningRepository
{
    /// <summary>Every game with its blunder count, first slip and evaluation after move 10 (from your side).</summary>
    Task<IReadOnlyList<OpeningGame>> LoadAsync(CancellationToken cancellationToken = default);
}
