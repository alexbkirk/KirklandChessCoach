using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Training;

/// <summary>
/// Builds the opening report card: per colour, the top 3 opening families, each with its 8 most-played variations.
/// A verdict compares an opening's win rate with your average for that colour, and its evaluation after move 10.
/// </summary>
public static class OpeningReportCalculator
{
    public const int FamiliesPerColor = 3;
    public const int VariationsPerFamily = 8;
    public const int MinGamesForVerdict = 20;
    public const double VerdictMarginPoints = 3;
    public const double FixEvalCp = -50;
    public const double KeepEvalCp = 0;

    private static readonly string[] Colors = ["white", "black"];

    public static OpeningReport Compute(IReadOnlyList<OpeningGame> games)
    {
        var colors = Colors.Select(color =>
        {
            var mine = games.Where(g => g.Color == color).ToList();
            if (mine.Count == 0)
            {
                return new ColorReport(color, 0, []);
            }

            var baseline = WinPercent(mine);
            var families = mine
                .GroupBy(g => g.Family)
                .OrderByDescending(f => f.Count())
                .Take(FamiliesPerColor)
                .Select(family =>
                {
                    var stats = StatsFor(family.ToList());
                    var variations = family
                        .GroupBy(g => g.Opening)
                        .OrderByDescending(v => v.Count())
                        .Take(VariationsPerFamily)
                        .Select(v => new VariationReport(v.Key, StatsFor(v.ToList())))
                        .ToList();
                    var (verdict, reason) = Verdict(stats, baseline);
                    return new FamilyReport(family.Key, stats, verdict, reason, variations);
                })
                .ToList();
            return new ColorReport(color, baseline, families);
        }).ToList();

        return new OpeningReport(colors);
    }

    public static OpeningStats StatsFor(IReadOnlyList<OpeningGame> games)
    {
        var slips = games.Where(g => g.FirstSlipMove is not null).Select(g => (double)g.FirstSlipMove!.Value).ToList();
        var evals = games.Where(g => g.EvalAt10 is not null).Select(g => (double)g.EvalAt10!.Value).ToList();
        return new OpeningStats(
            Games: games.Count,
            WinPercent: WinPercent(games),
            BlundersPerGame: games.Count == 0 ? 0 : games.Sum(g => g.Blunders) / (double)games.Count,
            MedianFirstSlipMove: Median(slips),
            SlipSharePercent: games.Count == 0 ? 0 : 100.0 * slips.Count / games.Count,
            AvgEvalAt10: evals.Count == 0 ? null : evals.Average());
    }

    public static (string Verdict, string Reason) Verdict(OpeningStats stats, double baseline)
    {
        if (stats.Games < MinGamesForVerdict)
        {
            return ("Watch", "fewer than 20 games");
        }

        if (stats.WinPercent <= baseline - VerdictMarginPoints || stats.AvgEvalAt10 is <= FixEvalCp)
        {
            return ("Fix", "played often but underperforming");
        }

        if (stats.WinPercent >= baseline + VerdictMarginPoints && (stats.AvgEvalAt10 is null || stats.AvgEvalAt10 >= KeepEvalCp))
        {
            return ("Keep", "doing better than your average");
        }

        return ("Watch", "about average");
    }

    private static double WinPercent(IReadOnlyList<OpeningGame> games) =>
        games.Count == 0 ? 0 : 100.0 * games.Count(g => g.Won) / games.Count;

    private static double? Median(List<double> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        values.Sort();
        var middle = values.Count / 2;
        return values.Count % 2 == 1 ? values[middle] : (values[middle - 1] + values[middle]) / 2;
    }
}
