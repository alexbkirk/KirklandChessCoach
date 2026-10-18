using ChessCoach.Core.Review;

namespace ChessCoach.Core.Abstractions;

public interface IGameReviewRepository
{
    /// <summary>The game with its stored analysis and the player's own notes, or null when there is no such game.</summary>
    Task<GameReview?> LoadAsync(string gameId, CancellationToken cancellationToken = default);

    /// <summary>Saves the player's mark on a move. A mark with no symbol and no note removes the saved one.</summary>
    Task SaveMoveAnnotationAsync(string gameId, int ply, string? glyph, string note, CancellationToken cancellationToken = default);

    /// <summary>Saves the note for the whole game. An empty note removes the saved one.</summary>
    Task SaveGameNoteAsync(string gameId, string note, CancellationToken cancellationToken = default);
}
