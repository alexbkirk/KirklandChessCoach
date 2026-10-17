using ChessCoach.Core.Domain;
using ChessCoach.Core.Stats;
using Record = ChessCoach.Core.Stats.Record;

namespace ChessCoach.Core.Tests;

public sealed class TimeControlLabelTests
{
    [Theory]
    [InlineData("600", "10 min")]
    [InlineData("1800", "30 min")]
    [InlineData("900+10", "15|10")]
    [InlineData("180+2", "3|2")]
    [InlineData("1/259200", "1/259200")]
    public void Time_controls_read_as_minutes_or_increments(string raw, string expected)
    {
        Assert.Equal(expected, TimeControlLabel.From(raw));
    }
}

public sealed class OverviewTests
{
    private static StatsGame Game(string id, int day, string color, string result, int rating, int plies = 60, string tc = "600") =>
        new(id, new DateOnly(2026, 1, 1).AddDays(day), "12:00:00", color, result, rating, 1200, tc, "Fam", "resignation", plies, "opp");

    [Fact]
    public void Totals_record_and_rating_extremes_come_from_the_games()
    {
        var games = new List<StatsGame>
        {
            Game("1", 0, "white", "win", 1000),
            Game("2", 1, "black", "loss", 1100),
            Game("3", 2, "white", "draw", 1050),
            Game("4", 3, "white", "win", 1020),
        };

        var overview = OverviewCalculator.Compute(games);

        Assert.Equal(new Record(Wins: 2, Draws: 1, Losses: 1), overview.Total);
        Assert.Equal(50, overview.Total.WinPercent, 6);
        Assert.Equal(1100, overview.PeakRating);
        Assert.Equal(1020, overview.LatestRating);
        Assert.Equal(30, overview.AverageMoves, 6);
        Assert.Equal(4, overview.Rating.Count);
    }

    [Fact]
    public void A_colour_filter_limits_the_totals_and_the_rating_line_but_not_the_colour_split()
    {
        var games = new List<StatsGame>
        {
            Game("1", 0, "white", "win", 1000),
            Game("2", 1, "black", "loss", 1100),
        };

        var white = OverviewCalculator.Compute(games, color: "white");

        Assert.Equal(1, white.Total.Games);
        Assert.Equal(1000, white.LatestRating);
        Assert.Single(white.Rating);
        Assert.Equal(1, white.ByColor.Single(s => s.Label == "Black").Record.Games);
    }

    [Fact]
    public void Time_control_splits_use_readable_labels_and_the_most_played_first()
    {
        var games = new List<StatsGame>
        {
            Game("1", 0, "white", "win", 1000, tc: "900+10"),
            Game("2", 1, "white", "win", 1000, tc: "600"),
            Game("3", 2, "white", "loss", 1000, tc: "600"),
        };

        var splits = OverviewCalculator.Compute(games).ByTimeControl;

        Assert.Equal(["10 min", "15|10"], splits.Select(s => s.Label));
        Assert.Equal(2, splits[0].Record.Games);
    }

    [Fact]
    public void An_empty_history_has_no_ratings()
    {
        var overview = OverviewCalculator.Compute([]);

        Assert.Equal(0, overview.Total.Games);
        Assert.Null(overview.PeakRating);
        Assert.Null(overview.LatestRating);
    }
}
