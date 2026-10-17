using ChessCoach.Core.Domain;
using ChessCoach.Core.Stats;

namespace ChessCoach.Core.Tests;

public sealed class OpeningsTableTests
{
    private static IEnumerable<StatsGame> Games(string family, string color, int wins, int losses, string idPrefix) =>
        Enumerable.Range(0, wins + losses).Select(i => new StatsGame(
            $"{idPrefix}{i}", new DateOnly(2026, 1, 1), "12:00:00", color, i < wins ? "win" : "loss", 1000, 1000, "600", family, "resignation", 40, "opp"));

    [Fact]
    public void Rows_are_ordered_by_games_played_and_carry_the_record()
    {
        var games = Games("Caro Kann Defense", "black", 6, 4, "a").Concat(Games("Pirc Defense", "black", 2, 10, "b")).Concat(Games("Slav Defense", "black", 5, 0, "c")).ToList();

        var rows = OpeningsTableCalculator.Compute(games, minGames: 1);

        Assert.Equal(["Pirc Defense", "Caro Kann Defense", "Slav Defense"], rows.Select(r => r.Family));
        Assert.Equal(12, rows[0].Record.Games);
        Assert.Equal(60, rows[1].Record.WinPercent, 6);
    }

    [Fact]
    public void Families_below_the_minimum_games_are_left_out()
    {
        var games = Games("Big", "white", 5, 5, "a").Concat(Games("Small", "white", 2, 1, "b")).ToList();

        var rows = OpeningsTableCalculator.Compute(games, minGames: 10);

        Assert.Equal(["Big"], rows.Select(r => r.Family));
    }

    [Fact]
    public void The_colour_filter_counts_only_games_as_that_colour()
    {
        var games = Games("Same", "white", 3, 0, "w").Concat(Games("Same", "black", 0, 4, "b")).ToList();

        var white = OpeningsTableCalculator.Compute(games, color: "white", minGames: 1).Single();
        var all = OpeningsTableCalculator.Compute(games, minGames: 1).Single();

        Assert.Equal(3, white.Record.Games);
        Assert.Equal(7, all.Record.Games);
    }

    [Fact]
    public void Ties_are_ordered_by_name_and_a_minimum_below_one_still_hides_empty_families()
    {
        var games = Games("B Opening", "white", 1, 0, "b").Concat(Games("A Opening", "white", 1, 0, "a")).ToList();

        var rows = OpeningsTableCalculator.Compute(games, minGames: 0);

        Assert.Equal(["A Opening", "B Opening"], rows.Select(r => r.Family));
    }
}
