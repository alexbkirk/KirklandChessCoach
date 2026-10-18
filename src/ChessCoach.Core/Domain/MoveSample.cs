namespace ChessCoach.Core.Domain;

/// <summary>One of the player's analysed moves (never the last move of a game), as the blunder stats need it.</summary>
/// <param name="EvalBefore">Engine evaluation in centipawns before the move, from White's point of view.</param>
/// <param name="Clock">Seconds left on the player's clock after the move, when the game recorded clocks.</param>
public sealed record MoveSample(string GameId, int Ply, string Color, int EvalBefore, int CpLoss, bool IsBlunder, double? Clock)
{
    /// <summary>The player was not already clearly lost (down more than a pawn) before the move.</summary>
    public bool Balanced => (Color == "white" ? EvalBefore : -EvalBefore) >= -100;
}
