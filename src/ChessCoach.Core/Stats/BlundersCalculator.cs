using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Stats;

/// <summary>Which blunders count, by how much time was left on the clock after the move.</summary>
public enum TimePressure
{
    All,

    /// <summary>A minute or less left.</summary>
    Low,

    /// <summary>More than a minute left.</summary>
    Normal,
}

public sealed record BlunderOptions(string? Color = null, bool BalancedOnly = true, TimePressure Pressure = TimePressure.All);

/// <param name="Blunders">Blunders in this group.</param>
/// <param name="Base">What the blunders are measured against: moves for the clock, games for move numbers.</param>
public sealed record BlunderBucket(string Label, int Blunders, int Base)
{
    public double Rate => Base == 0 ? 0 : (double)Blunders / Base;
}

public sealed record BlunderGroup(string Label, int Games, double BlundersPerGame, double AverageLoss);

public sealed record BlunderSummary(
    int Blunders,
    double PerGame,
    double AverageLoss,
    double GamesWithBlunderPercent,
    double ZeroBlunderWinPercent,
    double ThreePlusBlunderWinPercent);

public sealed record Blunders(
    BlunderSummary Summary,
    IReadOnlyList<BlunderBucket> ByClock,
    BlunderBucket LowTime,
    BlunderBucket EnoughTime,
    IReadOnlyList<BlunderBucket> ByMoveNumber,
    IReadOnlyList<BlunderGroup> ByTimeControl,
    IReadOnlyList<BlunderGroup> ByOpening);

public static class BlundersCalculator
{
    public const int DefaultMinGames = 10;

    private const int MoveBucketWidth = 5;
    private const int LastMoveBucketStart = 46;
    private static readonly int[] ClockEdges = [10, 30, 60, 120, 300];
    private static readonly string[] ClockLabels = ["10s or less", "10–30s", "30–60s", "1–2 min", "2–5 min", "over 5 min"];

    public static Blunders Compute(IReadOnlyList<StatsGame> games, IReadOnlyList<MoveSample> moves, BlunderOptions options, int minGames = DefaultMinGames)
    {
        var movesByGame = moves.ToLookup(m => m.GameId);
        var analysed = games
            .Where(g => options.Color is null || g.Color == options.Color)
            .Where(g => movesByGame[g.Id].Any())
            .ToList();
        var ids = analysed.Select(g => g.Id).ToHashSet();
        var mine = moves.Where(m => ids.Contains(m.GameId)).ToList();

        var counted = mine.Where(m => Counts(m, options)).ToList();
        var perGame = counted.GroupBy(m => m.GameId).ToDictionary(g => g.Key, g => g.Count());
        int BlundersIn(StatsGame g) => perGame.GetValueOrDefault(g.Id);
        double Loss(StatsGame g) => movesByGame[g.Id].Average(m => m.CpLoss);

        var clock = ClockBuckets(mine, counted);

        return new Blunders(
            Summary: Summarise(analysed, counted.Count, BlundersIn, Loss),
            ByClock: clock,
            LowTime: Combine(clock.Take(3)),
            EnoughTime: Combine(clock.Skip(3)),
            ByMoveNumber: MoveBuckets(analysed, counted),
            ByTimeControl: analysed
                .GroupBy(g => TimeControlLabel.From(g.TimeControl))
                .Select(g => Group(g.Key, g.ToList(), BlundersIn, Loss))
                .OrderByDescending(g => g.Games)
                .ToList(),
            ByOpening: analysed
                .GroupBy(g => g.Family)
                .Where(g => g.Count() >= minGames)
                .Select(g => Group(g.Key, g.ToList(), BlundersIn, Loss))
                .OrderByDescending(g => g.Games)
                .ToList());
    }

    private static bool Counts(MoveSample move, BlunderOptions options) =>
        move.IsBlunder
        && (!options.BalancedOnly || move.Balanced)
        && options.Pressure switch
        {
            TimePressure.Low => move.Clock is <= 60,
            TimePressure.Normal => move.Clock is > 60,
            _ => true,
        };

    private static int ClockBucket(double seconds)
    {
        var index = Array.FindIndex(ClockEdges, edge => seconds <= edge);
        return index < 0 ? ClockEdges.Length : index;
    }

    /// <summary>Blunders against all moves played with that much time left (every move, not only balanced ones).</summary>
    private static List<BlunderBucket> ClockBuckets(IReadOnlyList<MoveSample> mine, IReadOnlyList<MoveSample> counted)
    {
        var moves = new int[ClockLabels.Length];
        var blunders = new int[ClockLabels.Length];
        foreach (var move in mine.Where(m => m.Clock is not null)) moves[ClockBucket(move.Clock!.Value)]++;
        foreach (var move in counted.Where(m => m.Clock is not null)) blunders[ClockBucket(move.Clock!.Value)]++;
        return ClockLabels.Select((label, i) => new BlunderBucket(label, blunders[i], moves[i])).ToList();
    }

    private static BlunderBucket Combine(IEnumerable<BlunderBucket> buckets)
    {
        var list = buckets.ToList();
        return new BlunderBucket("", list.Sum(b => b.Blunders), list.Sum(b => b.Base));
    }

    /// <summary>Blunders by move number in bands of five, per game that reached the band.</summary>
    private static List<BlunderBucket> MoveBuckets(IReadOnlyList<StatsGame> analysed, IReadOnlyList<MoveSample> counted)
    {
        var buckets = new List<BlunderBucket>();
        for (var start = 1; start <= LastMoveBucketStart; start += MoveBucketWidth)
        {
            var last = start == LastMoveBucketStart;
            var end = last ? int.MaxValue : start + MoveBucketWidth - 1;
            var reached = analysed.Count(g => g.Plies / 2.0 >= start - 0.5);
            var blunders = counted.Count(m => MoveNumber(m.Ply) >= start && MoveNumber(m.Ply) <= end);
            buckets.Add(new BlunderBucket(last ? $"{start}+" : $"{start}–{end}", blunders, reached));
        }

        return buckets;
    }

    private static int MoveNumber(int ply) => (ply + 1) / 2;

    private static BlunderGroup Group(string label, List<StatsGame> games, Func<StatsGame, int> blunders, Func<StatsGame, double> loss) =>
        new(label, games.Count, games.Average(g => (double)blunders(g)), games.Average(loss));

    private static BlunderSummary Summarise(List<StatsGame> games, int total, Func<StatsGame, int> blunders, Func<StatsGame, double> loss)
    {
        if (games.Count == 0) return new BlunderSummary(0, 0, 0, 0, 0, 0);

        static double Percent(int part, int whole) => whole == 0 ? 0 : 100.0 * part / whole;
        var clean = games.Where(g => blunders(g) == 0).ToList();
        var rough = games.Where(g => blunders(g) >= 3).ToList();
        return new BlunderSummary(
            Blunders: total,
            PerGame: (double)total / games.Count,
            AverageLoss: games.Average(loss),
            GamesWithBlunderPercent: Percent(games.Count(g => blunders(g) > 0), games.Count),
            ZeroBlunderWinPercent: Percent(clean.Count(g => g.Result == "win"), clean.Count),
            ThreePlusBlunderWinPercent: Percent(rough.Count(g => g.Result == "win"), rough.Count));
    }
}
