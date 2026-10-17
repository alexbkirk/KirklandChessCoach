namespace ChessCoach.Core.Domain;

/// <summary>One of your games, reduced to what the opening report needs. Evaluations are from your side, in centipawns.</summary>
/// <param name="FirstSlipMove">Your first move (1 to 15) that lost at least 100 centipawns, or null.</param>
/// <param name="EvalAt10">Your evaluation after your 10th move, clamped to +/-1000, or null when the game ended earlier.</param>
public sealed record OpeningGame(
    string Id,
    string Color,
    string Family,
    string Opening,
    bool Won,
    int Blunders,
    int? FirstSlipMove,
    int? EvalAt10);

public sealed record OpeningStats(
    int Games,
    double WinPercent,
    double BlundersPerGame,
    double? MedianFirstSlipMove,
    double SlipSharePercent,
    double? AvgEvalAt10);

public sealed record VariationReport(string Opening, OpeningStats Stats);

public sealed record FamilyReport(string Family, OpeningStats Stats, string Verdict, string VerdictReason, IReadOnlyList<VariationReport> Variations);

public sealed record ColorReport(string Color, double WinPercent, IReadOnlyList<FamilyReport> Families);

public sealed record OpeningReport(IReadOnlyList<ColorReport> Colors);
