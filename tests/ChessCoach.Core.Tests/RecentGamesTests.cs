using ChessCoach.Core.Domain;
using ChessCoach.Core.Stats;

namespace ChessCoach.Core.Tests;

public sealed class RecentGamesTests
{
    private static StatsGame Game(string id, int day, string time = "12:00:00", string color = "white") =>
        new(id, new DateOnly(2026, 1, 1).AddDays(day), time, color, "win", 1000, 1000, "600", "Fam", "resignation", 60, "opp");

    [Fact]
    public void The_newest_games_come_first_with_the_time_of_day_breaking_ties()
    {
        var games = new[] { Game("old", 0), Game("late", 5, "20:00:00"), Game("early", 5, "08:00:00") };

        var recent = RecentGamesCalculator.Compute(games);

        Assert.Equal(["late", "early", "old"], recent.Games.Select(g => g.Id));
    }

    [Fact]
    public void Only_the_count_asked_for_is_returned_and_the_rest_is_counted()
    {
        var games = Enumerable.Range(0, 5).Select(i => Game($"g{i}", i)).ToList();

        var recent = RecentGamesCalculator.Compute(games, count: 2);

        Assert.Equal(["g4", "g3"], recent.Games.Select(g => g.Id));
        Assert.Equal(5, recent.Total);
        Assert.Equal(3, recent.Remaining);
    }

    [Fact]
    public void The_colour_filter_applies_before_counting()
    {
        var games = new[] { Game("w", 0), Game("b1", 1, color: "black"), Game("b2", 2, color: "black") };

        var recent = RecentGamesCalculator.Compute(games, "black", count: 1);

        Assert.Equal(["b2"], recent.Games.Select(g => g.Id));
        Assert.Equal(2, recent.Total);
    }

    [Fact]
    public void A_count_beyond_the_games_returns_them_all()
    {
        var recent = RecentGamesCalculator.Compute([Game("a", 0)], count: 50);

        Assert.Single(recent.Games);
        Assert.Equal(0, recent.Remaining);
    }
}
