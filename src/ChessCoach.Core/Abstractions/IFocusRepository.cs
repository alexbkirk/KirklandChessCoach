using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Abstractions;

public interface IFocusRepository
{
    /// <summary>All games, and every one of your blunders (final moves left out) with its explanation when it has one.</summary>
    Task<(IReadOnlyList<FocusGame> Games, IReadOnlyList<FocusBlunder> Blunders)> LoadAsync(CancellationToken cancellationToken = default);
}
