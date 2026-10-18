using ChessCoach.Core.Abstractions;

namespace ChessCoach.Core.Review;

/// <summary>Loads a game for review and saves the player's notes, checking what is saved.</summary>
public sealed class GameReviewService(IGameReviewRepository repository)
{
    public Task<GameReview?> LoadAsync(string gameId, CancellationToken cancellationToken = default) =>
        repository.LoadAsync(gameId, cancellationToken);

    /// <summary>Saves the symbol and note for a move. Empty text counts as no note; a symbol must be one of <see cref="ReviewTimeline.Glyphs"/>.</summary>
    public Task SaveMoveAnnotationAsync(string gameId, int ply, string? glyph, string? note, CancellationToken cancellationToken = default)
    {
        if (ply < 1) throw new ArgumentOutOfRangeException(nameof(ply), "A move number starts at 1.");
        if (!ReviewTimeline.IsValidGlyph(glyph)) throw new ArgumentException($"'{glyph}' is not a move symbol.", nameof(glyph));

        return repository.SaveMoveAnnotationAsync(gameId, ply, string.IsNullOrEmpty(glyph) ? null : glyph, Clean(note), cancellationToken);
    }

    public Task SaveGameNoteAsync(string gameId, string? note, CancellationToken cancellationToken = default) =>
        repository.SaveGameNoteAsync(gameId, Clean(note), cancellationToken);

    /// <summary>Trims the text and refuses one that is too long, so a note is never silently cut.</summary>
    private static string Clean(string? note)
    {
        var text = (note ?? "").Trim();
        if (text.Length > ReviewTimeline.MaxNoteLength)
        {
            throw new ArgumentException($"A note can be at most {ReviewTimeline.MaxNoteLength} characters.", nameof(note));
        }

        return text;
    }
}
