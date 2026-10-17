namespace ChessCoach.Core.Stats;

/// <summary>Winning chances: the expected score (0 to 100) for the side to move, from an evaluation in centipawns. The Lichess curve.</summary>
public static class WinChances
{
    /// <summary>Evaluations are stored from White's side; <paramref name="white"/> says which side's chances to return.</summary>
    public static double For(int whiteCentipawns, bool white) =>
        Percent(white ? whiteCentipawns : -whiteCentipawns);

    public static double Percent(int centipawns) =>
        50 + 50 * (2 / (1 + Math.Exp(-0.00368208 * centipawns)) - 1);
}
