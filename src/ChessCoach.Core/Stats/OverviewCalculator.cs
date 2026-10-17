using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Stats;

public sealed record Record(int Wins, int Draws, int Losses)
{
    public int Games => Wins + Draws + Losses;

    public double WinPercent => Games == 0 ? 0 : 100.0 * Wins / Games;
}

public sealed record RecordSplit(string Label, Record Record);

public sealed record RatingPoint(DateOnly Date, int Rating);

public sealed record Overview(
    Record Total,
    int? PeakRating,
    int? LatestRating,
    double AverageMoves,
    IReadOnlyList<RecordSplit> ByColor,
    IReadOnlyList<RecordSplit> ByTimeControl,
    IReadOnlyList<RatingPoint> Rating);

public static class OverviewCalculator
{
    private static readonly string[] Colors = ["white", "black"];

    /// <summary>Stats for all games, or only those played as <paramref name="color"/> ("white" or "black") when given.</summary>
    public static Overview Compute(IReadOnlyList<StatsGame> games, string? color = null)
    {
        var chosen = games
            .Where(g => color is null || g.Color == color)
            .OrderBy(g => g.Date).ThenBy(g => g.Time)
            .ToList();

        var total = RecordOf(chosen);
        var byColor = Colors
            .Select(c => new RecordSplit(c == "white" ? "White" : "Black", RecordOf(games.Where(g => g.Color == c).ToList())))
            .ToList();
        var byTimeControl = games
            .GroupBy(g => TimeControlLabel.From(g.TimeControl))
            .Select(g => new RecordSplit(g.Key, RecordOf(g.ToList())))
            .OrderByDescending(s => s.Record.Games)
            .ToList();

        return new Overview(
            Total: total,
            PeakRating: chosen.Count == 0 ? null : chosen.Max(g => g.MyRating),
            LatestRating: chosen.Count == 0 ? null : chosen[^1].MyRating,
            AverageMoves: chosen.Count == 0 ? 0 : chosen.Average(g => g.Plies) / 2.0,
            ByColor: byColor,
            ByTimeControl: byTimeControl,
            Rating: chosen.Select(g => new RatingPoint(g.Date, g.MyRating)).ToList());
    }

    private static Record RecordOf(IReadOnlyList<StatsGame> games) => new(
        Wins: games.Count(g => g.Result == "win"),
        Draws: games.Count(g => g.Result == "draw"),
        Losses: games.Count(g => g.Result == "loss"));
}
