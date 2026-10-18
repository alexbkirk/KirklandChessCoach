using ChessCoach.Core.Domain;
using ChessCoach.Core.Stats;
using Record = ChessCoach.Core.Stats.Record;

namespace ChessCoach.Core.Tests;

public sealed class HabitsTests
{
    private static StatsGame Game(string id, string date, string time, string result, string ending = "resignation", int myRating = 1000, int oppRating = 1000, string color = "white") =>
        new(id, DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture), time, color, result, myRating, oppRating, "600", "Fam", ending, 60, "opp");

    [Fact]
    public void Endings_are_counted_with_the_record_for_each_and_sorted_by_games()
    {
        var games = new List<StatsGame>
        {
            Game("1", "2026-01-01", "10:00:00", "win", "checkmate"),
            Game("2", "2026-01-01", "11:00:00", "loss", "resignation"),
            Game("3", "2026-01-01", "12:00:00", "loss", "resignation"),
        };

        var habits = HabitsCalculator.Compute(games);

        Assert.Equal("resignation", habits.Endings[0].Label);
        Assert.Equal(new Record(0, 0, 2), habits.Endings[0].Record);
        Assert.Equal(new Record(1, 0, 0), habits.Endings[1].Record);
    }

    [Fact]
    public void Hours_use_three_hour_buckets_and_keep_empty_ones()
    {
        var games = new List<StatsGame>
        {
            Game("1", "2026-01-01", "00:30:00", "win"),
            Game("2", "2026-01-01", "02:59:59", "loss"),
            Game("3", "2026-01-01", "21:10:00", "draw"),
        };

        var hours = HabitsCalculator.Compute(games).Hours;

        Assert.Equal(8, hours.Count);
        Assert.Equal(new Record(1, 0, 1), hours[0].Record);
        Assert.Equal(new Record(0, 0, 0), hours[1].Record);
        Assert.Equal(new Record(0, 1, 0), hours[7].Record);
    }

    [Fact]
    public void Rating_gap_is_opponent_minus_you_in_five_bands()
    {
        var games = new List<StatsGame>
        {
            Game("1", "2026-01-01", "10:00:00", "win", myRating: 1000, oppRating: 1150),
            Game("2", "2026-01-01", "11:00:00", "loss", myRating: 1000, oppRating: 1000),
            Game("3", "2026-01-01", "12:00:00", "win", myRating: 1000, oppRating: 900),
        };

        var gap = HabitsCalculator.Compute(games).RatingGap;

        Assert.Equal(new Record(0, 0, 1), gap.Single(r => r.Label == "within 30").Record);
        Assert.Equal(new Record(1, 0, 0), gap.Single(r => r.Label == "100+ higher").Record);
        Assert.Equal(new Record(1, 0, 0), gap.Single(r => r.Label == "30–100 lower").Record);
    }

    [Fact]
    public void Game_of_day_numbers_each_game_within_its_day()
    {
        var games = new List<StatsGame>
        {
            Game("1", "2026-01-01", "09:00:00", "win"),
            Game("2", "2026-01-01", "10:00:00", "loss"),
            Game("3", "2026-01-02", "09:00:00", "win"),
        };

        var day = HabitsCalculator.Compute(games).GameOfDay;

        Assert.Equal(new Record(2, 0, 0), day[0].Record);
        Assert.Equal(new Record(0, 0, 1), day[1].Record);
    }

    [Fact]
    public void Colour_filter_limits_every_view()
    {
        var games = new List<StatsGame>
        {
            Game("1", "2026-01-01", "09:00:00", "win", color: "white"),
            Game("2", "2026-01-01", "10:00:00", "loss", color: "black"),
        };

        var habits = HabitsCalculator.Compute(games, "black");

        Assert.Equal(1, habits.Endings.Sum(e => e.Record.Games));
        Assert.Equal(1, habits.GameOfDay[0].Record.Games);
    }
}
