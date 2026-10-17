using ChessCoach.Core.Domain;
using ChessCoach.Core.Stats;

namespace ChessCoach.Core.Tests;

public sealed class OpponentTests
{
    private static readonly PhaseBaseline Baseline = new(
        new Dictionary<string, PhaseRate> { [OpponentCalculator.Opening] = new(100, 60, 8) },
        new Dictionary<string, PhaseRate> { [OpponentCalculator.Opening] = new(100, 62, 7) });

    private static OpponentGame Game(string id, string date, string color, string result, string ending = "resignation", string family = "Caro Kann Defense", string opponent = "Rival") =>
        new(id, date, "12:00:00", color, result, ending, 1000, 1100, family, family, $"https://x/{id}", opponent);

    private static OpponentPly Ply(string game, int ply, bool mine, string san, int cpLoss = 0, string cls = "best", bool last = false, string? best = null, double? clock = null, string? note = null) =>
        new(game, ply, mine, san, san.ToLowerInvariant(), $"fen-{game}-{ply} w - -", best, best?.ToLowerInvariant(), cpLoss, cls, last, clock, note);

    private static StatsGame Stats(string opponent, string date, string result, string color = "white", int oppRating = 1100) =>
        new("id", DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture), "12:00:00", color, result, 1000, oppRating, "600", "Fam", "resignation", 40, opponent);

    [Fact]
    public void The_list_groups_names_without_case_and_uses_the_latest_spelling()
    {
        var games = new[]
        {
            Stats("rival", "2024-01-01", "win", oppRating: 1000),
            Stats("Rival", "2025-01-01", "loss", oppRating: 1200),
            Stats("RIVAL", "2023-01-01", "draw", color: "black", oppRating: 1100),
            Stats("once", "2025-02-02", "win"),
        };

        var row = Assert.Single(OpponentCalculator.List(games, minGames: 2));

        Assert.Equal("Rival", row.Name);
        Assert.Equal(3, row.Record.Games);
        Assert.Equal(1, row.Record.Draws);
        Assert.Equal("2025-01-01", row.LastDate);
        Assert.Equal(1100, row.AvgTheirRating);
        Assert.Equal(2, OpponentCalculator.List(games, color: "white", minGames: 2).Single().Record.Games);
        Assert.Equal(["once"], OpponentCalculator.List(games, minGames: 1, search: "ONC").Select(r => r.Name));
    }

    [Fact]
    public void Phases_follow_move_numbers()
    {
        Assert.Equal(OpponentCalculator.Opening, OpponentCalculator.PhaseOf(24));     // move 12
        Assert.Equal(OpponentCalculator.Middlegame, OpponentCalculator.PhaseOf(25));  // move 13
        Assert.Equal(OpponentCalculator.Late, OpponentCalculator.PhaseOf(61));        // move 31
    }

    [Fact]
    public void The_report_has_the_record_games_and_phase_rates_for_both_sides_without_final_moves()
    {
        var games = new[] { Game("g1", "2025-01-01", "white", "win"), Game("g2", "2025-03-01", "black", "loss", ending: "checkmate") };
        var plies = new[]
        {
            Ply("g1", 1, true, "e4", 0), Ply("g1", 2, false, "f6", 100, "mistake"), Ply("g1", 3, true, "Qh5", 300, "blunder"),
            Ply("g2", 1, false, "e4", 0), Ply("g2", 2, true, "f6", 50, "inaccuracy"), Ply("g2", 3, false, "Qh5", 900, "blunder", last: true),
        };

        var report = OpponentCalculator.Compute("rival", games, plies, Baseline);

        Assert.Equal("Rival", report.Name);
        Assert.Equal(1, report.Record.Wins);
        Assert.Equal(1, report.Record.Losses);
        Assert.Equal("2025-01-01", report.FirstDate);
        Assert.Equal("2025-03-01", report.LastDate);
        Assert.Equal(100, report.AvgRatingGap);
        Assert.Equal(["g2", "g1"], report.Games.Select(g => g.Game.GameId));
        Assert.Equal(1, report.Games[1].MyBlunders);
        Assert.Equal(0, report.Games[0].TheirBlunders);   // their Qh5 was the final move

        var opening = report.Phases.Single(p => p.Phase == OpponentCalculator.Opening);
        Assert.Equal(3, opening.You.Moves);
        Assert.Equal(350.0 / 3, opening.You.AvgCpLoss, 6);
        Assert.Equal(100.0 / 3, opening.You.BlunderPercent, 6);
        Assert.Equal(2, opening.Them.Moves);
        Assert.Equal(50, opening.Them.AvgCpLoss);
        Assert.Equal(60, opening.YouUsual.AvgCpLoss);
        Assert.Equal(7, opening.OpponentsUsual.BlunderPercent);
        Assert.Equal(PhaseRate.Empty, report.Phases.Single(p => p.Phase == OpponentCalculator.Late).YouUsual);

        Assert.Equal([("checkmate", 1), ("resignation", 1)], report.Endings.Select(e => (e.Label, e.Record.Games)));
        Assert.Equal(2, report.Openings.Count);   // the same family as White and as Black
    }

    [Fact]
    public void Their_blunders_show_whether_you_punished_them_missed_ones_first()
    {
        var games = new[] { Game("g1", "2025-01-01", "white", "win") };
        var plies = new[]
        {
            Ply("g1", 10, false, "Nd4", 300, "blunder", clock: 45),
            Ply("g1", 11, true, "Nxd4", 0, best: "Nxd4"),
            Ply("g1", 20, false, "Qe7", 500, "blunder"),
            Ply("g1", 21, true, "h3", 450, "blunder", best: "Rxe7", note: "h3 misses a free queen."),
            Ply("g1", 22, false, "Kh8", 0),
        };

        var report = OpponentCalculator.Compute("Rival", games, plies, Baseline);

        Assert.Equal(["Qe7", "Nd4"], report.TheirBlunders.Select(m => m.San));
        var missed = report.TheirBlunders[0];
        Assert.False(missed.Punished);
        Assert.Equal("h3", missed.Reply);
        Assert.Equal("Rxe7", missed.ReplyBestSan);
        Assert.True(report.TheirBlunders[1].Punished);
        Assert.Equal(45, report.TheirBlunders[1].Clock);

        var mine = Assert.Single(report.YourBlunders);
        Assert.Equal("h3", mine.San);
        Assert.Equal("h3 misses a free queen.", mine.NoteHeadline);
    }

    [Fact]
    public void Your_blunders_are_ranked_by_winning_chances_lost_not_by_the_capped_centipawn_loss()
    {
        var games = new[] { Game("g1", "2025-01-01", "white", "win") };
        var plies = new[]
        {
            // both capped at 1000 cp; the first loses 98 points of winning chances, the second only 20 (already winning by a lot)
            new OpponentPly("g1", 11, true, "Qh5", "d1h5", "f1", "Qd1", "d1d1", 1000, "blunder", false, 100, null, EvalBefore: 300, EvalAfter: -800),
            new OpponentPly("g1", 21, true, "Rd2", "d1d2", "f2", "Rd1", "d1d1", 1000, "blunder", false, 90, null, EvalBefore: 1500, EvalAfter: 700),
        };

        var report = OpponentCalculator.Compute("Rival", games, plies, Baseline);

        Assert.Equal(["Qh5", "Rd2"], report.YourBlunders.Select(m => m.San));
        Assert.InRange(report.YourBlunders[0].Before!.Value, 75, 77);
        Assert.InRange(report.YourBlunders[0].After!.Value, 4, 6);
    }

    [Fact]
    public void Winning_chances_use_the_movers_side_for_black()
    {
        var black = new OpponentPly("g", 10, false, "Nd4", "b8d4", "fen b KQkq -", null, null, 300, "blunder", false, null, null, EvalBefore: -200, EvalAfter: 150);

        var (before, after) = black.Chances!.Value;

        Assert.True(before > 50);   // -200 for White is good for Black
        Assert.True(after < 50);
    }

    [Fact]
    public void The_mover_comes_from_the_position_not_the_move_number()
    {
        // Ply 10 would normally be Black's move, but this game starts with Black to move, so White moves on even plies.
        var white = new OpponentPly("g", 10, false, "e4", "e2e4", "fen w KQkq -", null, null, 0, "best", false, null, null, EvalBefore: 200, EvalAfter: 200);

        Assert.True(white.Chances!.Value.Before > 50);
        Assert.Null(new OpponentPly("g", 10, false, "e4", "e2e4", "fen w KQkq -", null, null, 0, "best", false, null, null).Chances);
    }

    [Fact]
    public void A_game_with_no_analysed_moves_still_lists_with_no_blunders_or_phase_rates()
    {
        var games = new[] { Game("g1", "2025-01-01", "white", "draw", ending: "agreement") };

        var report = OpponentCalculator.Compute("Rival", games, [], Baseline);

        Assert.Equal(1, report.Record.Draws);
        Assert.Equal(new OpponentGameRow(games[0], 0, 0), Assert.Single(report.Games));
        Assert.All(report.Phases, p => Assert.Equal(PhaseRate.Empty, p.You));
        Assert.Empty(report.YourBlunders);
        Assert.Empty(report.TheirBlunders);
    }

    [Fact]
    public void A_reply_that_is_the_engines_best_move_counts_as_punishing_even_when_its_recorded_loss_is_large()
    {
        var games = new[] { Game("g1", "2025-01-01", "white", "win") };
        var plies = new[]
        {
            Ply("g1", 10, false, "Nd4", 300, "blunder"),
            Ply("g1", 11, true, "Nxd4", 250, "mistake", best: "Nxd4"),   // noisy evaluations: still the best move
        };

        var report = OpponentCalculator.Compute("Rival", games, plies, Baseline);

        Assert.True(Assert.Single(report.TheirBlunders).Punished);
    }

    [Fact]
    public void Opening_slips_are_found_for_each_side_within_the_first_fifteen_moves()
    {
        var games = new[] { Game("g1", "2025-01-01", "white", "win"), Game("g2", "2025-01-02", "white", "loss") };
        var plies = new List<OpponentPly>();
        foreach (var id in new[] { "g1", "g2" })
        {
            plies.Add(new(id, 1, true, "e4", "e2e4", "start w KQkq -", "e4", "e2e4", 0, "best", false, null, null));
            plies.Add(new(id, 2, false, "f6", "f7f6", "after-e4 b KQkq -", "e5", "e7e5", 150, "mistake", false, null, null));
            plies.Add(new(id, 32, true, "Qh5", "d1h5", $"late-{id} w - -", "Qd1", "d1d1", 400, "blunder", false, null, null));
        }

        var report = OpponentCalculator.Compute("Rival", games, plies, Baseline);

        var theirs = Assert.Single(report.TheirOpeningSlips);
        Assert.Equal("f6", theirs.MistakeSan);
        Assert.Equal(2, theirs.Slips);
        Assert.Equal("e5", theirs.BestSan);
        Assert.Equal("1.e4", theirs.Path);
        Assert.Empty(report.YourOpeningSlips);   // your Qh5 came after move 15
    }

    [Fact]
    public void No_games_gives_an_empty_report_with_the_requested_name()
    {
        var report = OpponentCalculator.Compute("nobody", [], [], Baseline);

        Assert.Equal("nobody", report.Name);
        Assert.Equal(0, report.Record.Games);
        Assert.Empty(report.Games);
    }
}
