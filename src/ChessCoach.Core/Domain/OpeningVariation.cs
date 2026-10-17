using ChessCoach.Core.Stats;

namespace ChessCoach.Core.Domain;

/// <summary>Which games a variation page covers: your games as <paramref name="Color"/> in one opening (exact name) or a whole family.</summary>
public sealed record OpeningScope(string Color, string? Family, string? Opening)
{
    public string Name => Opening ?? Family ?? "";
}

/// <summary>One of your games in the scope.</summary>
public sealed record VariationGame(string GameId, string Result, string Date, string Opponent, string Url);

/// <summary>One move (either side) from the first 15 moves of a game in the scope. <paramref name="Fen"/> is the position before it.</summary>
/// <param name="CpLoss">Centipawns the move lost, from the analysis (capped at 1000).</param>
public sealed record VariationPly(string GameId, int Ply, string Fen, string San, string Uci, bool IsMine, string? BestSan, string? BestUci, int CpLoss);

/// <summary>A move of your most common line through the variation.</summary>
/// <param name="Games">Games that played this move at this point.</param>
/// <param name="Reached">Games that reached the position before it.</param>
/// <param name="AvgCpLoss">Your average loss with this move (your moves only).</param>
/// <param name="Slips">How often this move lost 100 centipawns or more (your moves only).</param>
public sealed record LineMove(int Ply, string Fen, string San, string Uci, bool IsMine, int Games, int Reached, double AvgCpLoss, int Slips, string? BestSan, string? BestUci);

/// <summary>A position from your games where you have gone wrong, to review and practise.</summary>
/// <param name="Key">The position (FEN without the move counters); identifies it for practice history.</param>
/// <param name="Path">The moves that reach it, e.g. "1.e4 c6 2.Nc3 d5 3.Nf3".</param>
/// <param name="Reached">Times you have had this position (your move).</param>
/// <param name="Slips">Times your move here lost 100 centipawns or more.</param>
/// <param name="MistakeSan">Your most common move among those slips, with how often you played it and its average loss.</param>
/// <param name="Accept">UCI moves that count as right: the engine's best, plus any move you played here that lost 30 centipawns or less.</param>
public sealed record TroubleSpot(
    string Key,
    string Fen,
    int Ply,
    string Path,
    int Reached,
    int Slips,
    string MistakeSan,
    int MistakeCount,
    double MistakeAvgCpLoss,
    string BestSan,
    string BestUci,
    IReadOnlyList<string> Accept,
    string ExampleUrl);

/// <summary>The variation page: your record, your usual line and the positions where you go wrong.</summary>
public sealed record VariationReview(OpeningScope Scope, Record Record, IReadOnlyList<LineMove> Line, IReadOnlyList<TroubleSpot> Trouble);

/// <summary>Your practice history for one trouble spot.</summary>
public sealed record PracticeTally(int Tries, int Correct, bool LastCorrect);
