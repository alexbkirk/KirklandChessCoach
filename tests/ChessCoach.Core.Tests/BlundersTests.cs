using ChessCoach.Core.Domain;
using ChessCoach.Core.Stats;

namespace ChessCoach.Core.Tests;

public sealed class BlundersTests
{
    private static StatsGame Game(string id, string result = "win", string color = "white", int plies = 60, string family = "Fam", string tc = "600") =>
        new(id, new DateOnly(2026, 1, 1), "12:00:00", color, result, 1000, 1000, tc, family, "resignation", plies, "opp");

    private static MoveSample Move(string game, int ply, bool blunder = false, int evalBefore = 0, double? clock = 300, int loss = 0, string color = "white") =>
        new(game, ply, color, evalBefore, loss, blunder, clock);

    private static readonly BlunderOptions Everything = new(BalancedOnly: false);

    [Fact]
    public void A_move_is_balanced_unless_the_player_was_already_a_pawn_down()
    {
        Assert.True(Move("g", 1, evalBefore: -100).Balanced);
        Assert.False(Move("g", 1, evalBefore: -101).Balanced);
        Assert.False(Move("g", 2, evalBefore: 101, color: "black").Balanced);
        Assert.False(Move("g", 2, evalBefore: 500, color: "black").Balanced);
        Assert.True(Move("g", 2, evalBefore: -500, color: "black").Balanced);
    }

    [Fact]
    public void Balanced_only_leaves_out_blunders_made_in_lost_positions()
    {
        var games = new[] { Game("1") };
        var moves = new[] { Move("1", 11, blunder: true), Move("1", 13, blunder: true, evalBefore: -400) };

        Assert.Equal(1, BlundersCalculator.Compute(games, moves, new BlunderOptions()).Summary.Blunders);
        Assert.Equal(2, BlundersCalculator.Compute(games, moves, Everything).Summary.Blunders);
    }

    [Fact]
    public void Time_pressure_keeps_only_blunders_by_the_clock_after_the_move()
    {
        var games = new[] { Game("1") };
        var moves = new[] { Move("1", 11, blunder: true, clock: 40), Move("1", 13, blunder: true, clock: 200), Move("1", 15, blunder: true, clock: null) };

        Assert.Equal(1, BlundersCalculator.Compute(games, moves, new BlunderOptions(Pressure: TimePressure.Low)).Summary.Blunders);
        Assert.Equal(1, BlundersCalculator.Compute(games, moves, new BlunderOptions(Pressure: TimePressure.Normal)).Summary.Blunders);
        Assert.Equal(3, BlundersCalculator.Compute(games, moves, new BlunderOptions(Pressure: TimePressure.All)).Summary.Blunders);
    }

    [Fact]
    public void The_clock_rate_is_blunders_over_every_move_played_with_that_much_time_left()
    {
        var games = new[] { Game("1") };
        var moves = new[]
        {
            Move("1", 1, clock: 5), Move("1", 3, blunder: true, clock: 8), Move("1", 5, clock: 400), Move("1", 7, clock: 450),
        };

        var clock = BlundersCalculator.Compute(games, moves, Everything);

        Assert.Equal(new BlunderBucket("10s or less", 1, 2), clock.ByClock[0]);
        Assert.Equal(0.5, clock.ByClock[0].Rate);
        Assert.Equal(2, clock.ByClock[5].Base);
        Assert.Equal(1, clock.LowTime.Blunders);
        Assert.Equal(2, clock.EnoughTime.Base);
    }

    [Fact]
    public void The_clock_rate_measures_blunders_against_moves_under_the_same_filters()
    {
        var games = new[] { Game("1") };
        var moves = new[]
        {
            Move("1", 1, blunder: true, clock: 5),
            Move("1", 3, clock: 6),
            Move("1", 5, blunder: true, evalBefore: -400, clock: 7),   // lost position
            Move("1", 7, evalBefore: -400, clock: 8),                  // lost position
            Move("1", 9, clock: 200),
        };

        var balanced = BlundersCalculator.Compute(games, moves, new BlunderOptions());
        Assert.Equal(new BlunderBucket("10s or less", 1, 2), balanced.ByClock[0]);

        var all = BlundersCalculator.Compute(games, moves, Everything);
        Assert.Equal(new BlunderBucket("10s or less", 2, 4), all.ByClock[0]);

        var low = BlundersCalculator.Compute(games, moves, new BlunderOptions(Pressure: TimePressure.Low));
        Assert.Equal(0, low.ByClock[3].Base + low.ByClock[4].Base + low.ByClock[5].Base);
    }

    [Fact]
    public void Clock_edges_belong_to_the_lower_bucket()
    {
        var games = new[] { Game("1") };
        var moves = new[] { Move("1", 1, clock: 10), Move("1", 3, clock: 10.5), Move("1", 5, clock: 300), Move("1", 7, clock: 300.5) };

        var buckets = BlundersCalculator.Compute(games, moves, Everything).ByClock;

        Assert.Equal(1, buckets[0].Base);
        Assert.Equal(1, buckets[1].Base);
        Assert.Equal(1, buckets[4].Base);
        Assert.Equal(1, buckets[5].Base);
    }

    [Fact]
    public void Move_number_buckets_divide_by_the_games_that_reached_them()
    {
        // game 1 lasts 30 moves (60 plies), game 2 only 4 moves
        var games = new[] { Game("1", plies: 60), Game("2", plies: 8) };
        var moves = new[] { Move("1", 11, blunder: true), Move("2", 1), Move("1", 1), Move("2", 7, blunder: true) };

        var buckets = BlundersCalculator.Compute(games, moves, Everything).ByMoveNumber;

        Assert.Equal("1–5", buckets[0].Label);
        Assert.Equal(new BlunderBucket("1–5", 1, 2), buckets[0]);
        Assert.Equal(new BlunderBucket("6–10", 1, 1), buckets[1]);
        Assert.Equal("46+", buckets[^1].Label);
    }

    [Fact]
    public void Games_without_analysis_are_left_out_and_the_colour_filter_applies()
    {
        var games = new[] { Game("1"), Game("2", color: "black"), Game("3") };
        var moves = new[] { Move("1", 1, blunder: true), Move("2", 2, blunder: true, color: "black") };

        Assert.Equal(2, BlundersCalculator.Compute(games, moves, Everything).ByTimeControl.Sum(g => g.Games));
        Assert.Equal(1, BlundersCalculator.Compute(games, moves, new BlunderOptions(Color: "black", BalancedOnly: false)).ByTimeControl.Sum(g => g.Games));
    }

    [Fact]
    public void Summary_compares_clean_games_with_games_of_three_or_more_blunders()
    {
        var games = new[] { Game("clean", "win"), Game("rough", "loss"), Game("rough2", "win") };
        var moves = new List<MoveSample> { Move("clean", 1, loss: 10) };
        moves.AddRange(Enumerable.Range(0, 3).Select(i => Move("rough", 1 + i * 2, blunder: true, loss: 300)));
        moves.AddRange(Enumerable.Range(0, 3).Select(i => Move("rough2", 1 + i * 2, blunder: true, loss: 300)));

        var summary = BlundersCalculator.Compute(games, moves, Everything).Summary;

        Assert.Equal(6, summary.Blunders);
        Assert.Equal(2.0, summary.PerGame, 6);
        Assert.Equal(100, summary.ZeroBlunderWinPercent);
        Assert.Equal(50, summary.ThreePlusBlunderWinPercent);
        Assert.Equal(200.0 / 3, summary.GamesWithBlunderPercent, 6);
    }

    [Fact]
    public void Openings_below_the_minimum_games_are_hidden()
    {
        var games = Enumerable.Range(0, 10).Select(i => Game($"a{i}", family: "Common")).Append(Game("b", family: "Rare")).ToList();
        var moves = games.Select(g => Move(g.Id, 1)).ToList();

        var openings = BlundersCalculator.Compute(games, moves, Everything).ByOpening;

        Assert.Equal(["Common"], openings.Select(o => o.Label));
    }
}
