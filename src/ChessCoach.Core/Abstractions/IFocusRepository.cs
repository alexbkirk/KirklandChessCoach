using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Abstractions;

public interface IFocusRepository
{
    /// <summary>All games (oldest first) and the balanced, non-final-move blunders with their tags.</summary>
    Task<(IReadOnlyList<FocusGame> Games, IReadOnlyList<FocusBlunder> Blunders)> LoadAsync(CancellationToken cancellationToken = default);
}
