using ChessCoach.Core.Domain;
using ChessCoach.Core.Stats;

namespace ChessCoach.Core.Tests;

public sealed class TournamentPrepTests
{
    private static StatsGame Game(string id, string family, string color, string result, string date = "2026-01-01", string time = "12:00:00", string url = "https://x") =>
        new(id, DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture), time, color, result, 1000, 1000, "600", family, "resignation", 40, "opp", url);

    private static MoveSample Move(string gameId, bool blunder) =>
        new(gameId, 1, "white", 0, blunder ? 300 : 0, blunder, 120);

    [Fact]
    public void Empty_input_gives_zeros_and_empty_lists()
    {
        var prep = TournamentPrepCalculator.Compute([], []);

        Assert.Equal(0, prep.Games);
        Assert.Equal(0, prep.Record.Games);
        Assert.Empty(prep.Openings);
        Assert.Empty(prep.Recent);
        Assert.Equal(0, prep.OverTheBoard);
        Assert.Equal(0, prep.BlunderRate);
    }

    [Fact]
    public void One_game_is_counted_with_no_cutoff()
    {
        var prep = TournamentPrepCalculator.Compute([Game("a", "Caro Kann Defense", "black", "win", url: "")], []);

        Assert.Equal(1, prep.Games);
        Assert.Equal(1, prep.Record.Wins);
        Assert.Equal(1, prep.OverTheBoard);
        Assert.Single(prep.Openings);
        Assert.Single(prep.Recent);
    }

    [Fact]
    public void Openings_are_sorted_by_games_then_name()
    {
        var games = new[]
        {
            Game("a1", "Slav", "white", "win"),
            Game("b1", "Caro Kann", "white", "loss"),
            Game("b2", "Caro Kann", "white", "draw"),
            Game("c1", "Alekhine", "black", "win"),
            Game("c2", "Alekhine", "black", "win"),
        };

        var prep = TournamentPrepCalculator.Compute(games, []);

        Assert.Equal(["Alekhine", "Caro Kann", "Slav"], prep.Openings.Select(r => r.Family));
        Assert.Equal(2, prep.Openings[0].Record.Wins);
        Assert.Equal(1, prep.Openings[1].Record.Draws);
        Assert.Equal(1, prep.Openings[1].Record.Losses);
    }

    [Fact]
    public void Recent_is_newest_first_and_capped_at_five()
    {
        var games = Enumerable.Range(0, 7)
            .Select(i => Game($"g{i}", "Open", "white", "win", date: $"2026-01-0{i + 1}"))
            .ToList();

        var prep = TournamentPrepCalculator.Compute(games, []);

        Assert.Equal(5, prep.Recent.Count);
        Assert.Equal(new DateOnly(2026, 1, 7), prep.Recent[0].Date);
        Assert.Equal(new DateOnly(2026, 1, 3), prep.Recent[4].Date);
    }

    [Fact]
    public void Blunder_rate_with_no_moves_is_zero()
    {
        var prep = TournamentPrepCalculator.Compute([Game("a", "Open", "white", "win")], []);

        Assert.Equal(0, prep.BlunderRate);
        Assert.False(double.IsNaN(prep.BlunderRate));
    }

    [Fact]
    public void Blunder_rate_counts_only_moves_of_the_given_games()
    {
        var games = new[] { Game("a", "Open", "white", "win") };
        var moves = new[] { Move("a", true), Move("a", false), Move("a", false), Move("other", true) };

        var prep = TournamentPrepCalculator.Compute(games, moves);

        Assert.Equal(1.0 / 3.0, prep.BlunderRate, 6);
    }
}
