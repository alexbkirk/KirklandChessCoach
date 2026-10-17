using ChessCoach.Core.Domain;
using ChessCoach.Core.Training;

namespace ChessCoach.Core.Tests;

public sealed class OpeningReportTests
{
    private static OpeningGame Game(string id, string color, string family, string opening, bool won = false, int blunders = 0, int? slip = null, int? eval10 = null) =>
        new(id, color, family, opening, won, blunders, slip, eval10);

    /// <summary>n games of one opening; the first <paramref name="wins"/> are wins.</summary>
    private static IEnumerable<OpeningGame> Many(string prefix, string color, string family, string opening, int n, int wins, int? slip = null, int? eval10 = null) =>
        Enumerable.Range(0, n).Select(i => Game($"{prefix}{i}", color, family, opening, won: i < wins, slip: slip, eval10: eval10));

    [Fact]
    public void Only_the_top_three_families_per_colour_are_shown()
    {
        var games = new List<OpeningGame>();
        games.AddRange(Many("a", "white", "Fam A", "Fam A", 9, 0));
        games.AddRange(Many("b", "white", "Fam B", "Fam B", 8, 0));
        games.AddRange(Many("c", "white", "Fam C", "Fam C", 7, 0));
        games.AddRange(Many("d", "white", "Fam D", "Fam D", 2, 0));

        var white = OpeningReportCalculator.Compute(games).Colors.Single(c => c.Color == "white");

        Assert.Equal(["Fam A", "Fam B", "Fam C"], white.Families.Select(f => f.Family));
    }

    [Fact]
    public void Only_the_eight_most_played_variations_are_shown()
    {
        var games = Enumerable.Range(1, 10).SelectMany(v => Many($"v{v}_", "white", "Fam", $"Fam Var{v}", n: 11 - v, wins: 0)).ToList();

        var family = OpeningReportCalculator.Compute(games).Colors.Single(c => c.Color == "white").Families.Single();

        Assert.Equal(8, family.Variations.Count);
        Assert.Equal("Fam Var1", family.Variations[0].Opening);
        Assert.Equal(10, family.Variations[0].Stats.Games);
    }

    [Fact]
    public void Colours_are_kept_apart_and_the_baseline_is_per_colour()
    {
        var games = Many("w", "white", "Fam", "Fam", 20, wins: 16)
            .Concat(Many("b", "black", "Fam", "Fam", 20, wins: 4)).ToList();

        var report = OpeningReportCalculator.Compute(games);

        Assert.Equal(80, report.Colors.Single(c => c.Color == "white").WinPercent, 6);
        Assert.Equal(20, report.Colors.Single(c => c.Color == "black").WinPercent, 6);
    }

    [Fact]
    public void An_opening_well_below_your_average_with_enough_games_is_a_fix()
    {
        // White baseline: 50 games, 20 wins (40%). "Fam Bad": 20 games, 6 wins (30%), which is 10 points below.
        var games = Many("x", "white", "Fam", "Fam Other", 30, wins: 14).Concat(Many("y", "white", "Fam", "Fam Bad", 20, wins: 6)).ToList();

        var report = OpeningReportCalculator.Compute(games).Colors.Single(c => c.Color == "white");
        var bad = report.Families.Single().Variations.Single(v => v.Opening == "Fam Bad");

        Assert.Equal(40, report.WinPercent, 6);
        Assert.Equal(30, bad.Stats.WinPercent, 6);
        Assert.Equal("Fix", OpeningReportCalculator.Verdict(bad.Stats, report.WinPercent).Verdict);
        Assert.Equal("Watch", report.Families.Single().Verdict);   // the family as a whole sits at the baseline
    }

    [Fact]
    public void A_bad_evaluation_after_move_ten_is_a_fix_even_with_an_average_win_rate()
    {
        var stats = new OpeningStats(Games: 25, WinPercent: 50, BlundersPerGame: 2, MedianFirstSlipMove: 9, SlipSharePercent: 90, AvgEvalAt10: -60);

        Assert.Equal("Fix", OpeningReportCalculator.Verdict(stats, baseline: 50).Verdict);
    }

    [Fact]
    public void A_strong_opening_with_a_healthy_evaluation_is_a_keep()
    {
        var stats = new OpeningStats(Games: 25, WinPercent: 60, BlundersPerGame: 2, MedianFirstSlipMove: 12, SlipSharePercent: 80, AvgEvalAt10: 30);

        Assert.Equal("Keep", OpeningReportCalculator.Verdict(stats, baseline: 50).Verdict);
    }

    [Fact]
    public void Fewer_than_twenty_games_is_always_watch()
    {
        var stats = new OpeningStats(Games: 19, WinPercent: 0, BlundersPerGame: 5, MedianFirstSlipMove: 3, SlipSharePercent: 100, AvgEvalAt10: -500);

        Assert.Equal("Watch", OpeningReportCalculator.Verdict(stats, baseline: 50).Verdict);
    }

    [Fact]
    public void Stats_give_the_median_first_slip_and_the_share_of_games_with_a_slip()
    {
        var games = new List<OpeningGame>
        {
            Game("1", "white", "Fam", "Fam", slip: 6),
            Game("2", "white", "Fam", "Fam", slip: 10),
            Game("3", "white", "Fam", "Fam", slip: 8),
            Game("4", "white", "Fam", "Fam"),
        };

        var stats = OpeningReportCalculator.StatsFor(games);

        Assert.Equal(8, stats.MedianFirstSlipMove);
        Assert.Equal(75, stats.SlipSharePercent, 6);
    }

    [Fact]
    public void Blunders_per_game_is_the_average_over_the_games()
    {
        var games = new List<OpeningGame> { Game("1", "white", "Fam", "Fam", blunders: 4), Game("2", "white", "Fam", "Fam", blunders: 2) };

        Assert.Equal(3, OpeningReportCalculator.StatsFor(games).BlundersPerGame, 6);
    }
}
