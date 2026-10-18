namespace ChessCoach.Core.Review;

public sealed record ReviewGame(
    string Id,
    string Url,
    DateOnly Date,
    string Color,
    string Result,
    string Ending,
    string TimeControl,
    int MyRating,
    int OppRating,
    string Opponent,
    string Family,
    string Opening);

/// <summary>One half-move of a game with the engine's stored verdict on it.</summary>
/// <param name="Ply">1 for White's first move, 2 for Black's reply, and so on.</param>
/// <param name="EvalBefore">Centipawns before the move, from White's side (about 10000 means mate).</param>
/// <param name="EvalAfter">Centipawns after the move, from White's side.</param>
/// <param name="Class">best, good, inaccuracy, mistake or blunder; empty when the move was not graded.</param>
/// <param name="Clock">Seconds left on the mover's clock after the move, when recorded.</param>
public sealed record ReviewMove(
    int Ply,
    string Color,
    bool IsMine,
    bool IsLast,
    string San,
    string Uci,
    string FenBefore,
    string? BestUci,
    string? BestSan,
    int EvalBefore,
    int EvalAfter,
    int CpLoss,
    string Class,
    double? Clock);

/// <summary>The player's own mark on a move: a symbol such as "?!" and/or a note.</summary>
public sealed record MoveAnnotation(int Ply, string? Glyph, string Note);

public sealed record GameReview(
    ReviewGame Game,
    IReadOnlyList<ReviewMove> Moves,
    IReadOnlyList<MoveAnnotation> Annotations,
    string GameNote);
