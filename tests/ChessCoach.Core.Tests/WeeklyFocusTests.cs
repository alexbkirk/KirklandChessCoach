using ChessCoach.Core.Domain;
using ChessCoach.Core.Training;

namespace ChessCoach.Core.Tests;

public sealed class WeeklyFocusTests
{
    private static readonly DateOnly Latest = new(2026, 10, 16);

    /// <summary>A game played <paramref name="daysBeforeLatest"/> days before the latest one.</summary>
    private static FocusGame Game(string id, int daysBeforeLatest) => new(id, Latest.AddDays(-daysBeforeLatest), "opp", $"https://x/{id}");

    private static FocusBlunder Blunder(string gameId, int ply, params string[] tags) => new(gameId, ply, tags);

    private static List<FocusGame> Recent(int count) => Enumerable.Range(0, count).Select(i => Game($"g{i}", i)).ToList();

    [Fact]
    public void Uses_the_last_30_days_when_the_window_has_at_least_15_games()
    {
        var games = Recent(20).Concat(Enumerable.Range(0, 10).Select(i => Game($"old{i}", 100 + i))).ToList();

        var focus = WeeklyFocusCalculator.Compute(games, []);

        Assert.Equal("the last 30 days", focus.Window);
        Assert.Equal(20, focus.GameCount);
    }

    [Fact]
    public void Falls_back_to_the_last_50_games_when_the_window_is_thin()
    {
        var games = Enumerable.Range(0, 60).Select(i => Game($"g{i}", i * 10)).ToList();   // one game every 10 days

        var focus = WeeklyFocusCalculator.Compute(games, []);

        Assert.Equal("your last 50 games", focus.Window);
        Assert.Equal(50, focus.GameCount);
    }

    [Fact]
    public void The_focus_is_the_most_frequent_leak_and_positional_slips_never_win()
    {
        var blunders = new List<FocusBlunder>
        {
            Blunder("g0", 3, "other"), Blunder("g1", 5, "other"), Blunder("g2", 7, "other"), Blunder("g3", 9, "other"),
            Blunder("g4", 11, "missed_tactic"), Blunder("g5", 13, "missed_tactic"),
        };

        var focus = WeeklyFocusCalculator.Compute(Recent(20), blunders);

        Assert.Equal("missed_tactic", focus.TopKey);
        Assert.Equal("Missed tactics", focus.Title);
    }

    [Fact]
    public void With_only_positional_slips_there_is_no_focus()
    {
        var focus = WeeklyFocusCalculator.Compute(Recent(20), [Blunder("g0", 3, "other")]);

        Assert.Null(focus.TopKey);
        Assert.Null(focus.Title);
        Assert.Equal(TagCatalog.All.Count, focus.Stats.Count);
    }

    [Fact]
    public void The_window_rate_is_compared_with_the_lifetime_rate()
    {
        // 20 recent games with 4 missed tactics (0.2 per game). Lifetime: 6 tactics over 40 games (0.15 per game).
        var games = Recent(20).Concat(Enumerable.Range(0, 20).Select(i => Game($"old{i}", 100 + i))).ToList();
        var blunders = Enumerable.Range(0, 4).Select(i => Blunder($"g{i}", 5, "missed_tactic"))
            .Concat(Enumerable.Range(0, 2).Select(i => Blunder($"old{i}", 5, "missed_tactic")))
            .ToList();

        var stat = WeeklyFocusCalculator.Compute(games, blunders).Stats.Single(s => s.Key == "missed_tactic");

        Assert.Equal(4, stat.Count);
        Assert.Equal(0.2, stat.PerGame, 6);
        Assert.Equal((0.2 / 0.15 - 1) * 100, stat.DeltaPercent!.Value, 6);
    }

    [Fact]
    public void Examples_are_the_five_most_recent_games_with_the_leak_latest_move_first()
    {
        var blunders = Enumerable.Range(0, 20).Select(i => Blunder($"g{i}", 10, "missed_mate")).ToList();

        var focus = WeeklyFocusCalculator.Compute(Recent(20), blunders);

        Assert.Equal(5, focus.Examples.Count);
        Assert.Equal("https://x/g0", focus.Examples[0].Url);
        Assert.Equal("https://x/g4", focus.Examples[4].Url);
        Assert.Equal(5, focus.Examples[0].Move);
    }

    [Fact]
    public void No_games_gives_an_empty_focus()
    {
        var focus = WeeklyFocusCalculator.Compute([], []);

        Assert.Equal(0, focus.GameCount);
        Assert.Null(focus.TopKey);
    }
}
