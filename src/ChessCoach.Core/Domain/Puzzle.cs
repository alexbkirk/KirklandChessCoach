namespace ChessCoach.Core.Domain;

/// <summary>The coaching explanation for one blunder, as produced by analysis/coach.py (see docs/COACHING.md).</summary>
/// <param name="Motif">The tactic behind the blunder, e.g. "Knight fork allowed" or "Missed back-rank mate"; empty when none was found.</param>
/// <param name="Pattern">How often the same tactic is behind your other noted blunders; empty when it is rare.</param>
public sealed record CoachNote(
    string Headline,
    IReadOnlyList<string> Why,
    string BetterSan,
    string BetterLine,
    string Motif,
    string Pattern,
    string MaiaText,
    string EvalBefore,
    string EvalAfter);

/// <summary>A position from one of your games where you should find the best move.</summary>
/// <param name="PositionId">Stable id: "&lt;gameId&gt;_&lt;ply&gt;".</param>
/// <param name="Fen">Position before the blunder; the side to move is the one you play.</param>
/// <param name="Color">"white" or "black": the side that blundered and must answer.</param>
/// <param name="Accept">UCI moves that count as correct (best move plus anything within 50 centipawns).</param>
/// <param name="CpLoss">Centipawns lost by the move you actually played (capped at 1000).</param>
public sealed record Puzzle(
    string PositionId,
    string GameId,
    int Ply,
    string Fen,
    string Color,
    string PlayedSan,
    string BestUci,
    IReadOnlyList<string> Accept,
    int CpLoss,
    string Date,
    string Opponent,
    string Url,
    CoachNote Note);
