using ChessCoach.Core.Domain;
using ChessCoach.Core.Training;

namespace ChessCoach.Core.Tests;

public sealed class WeeklyFocusTests
{
    private static readonly DateOnly Latest = new(2026, 10, 16);

    /// <summary>A game played <paramref name="daysBeforeLatest"/> days before the latest one.</summary>
    private static FocusGame Game(string id, int daysBeforeLatest) => new(id, Latest.AddDays(-daysBeforeLatest), "opp", $"https://x/{id}");

    /// <summary>An explained blunder; <paramref name="swing"/> null means it has no note yet.</summary>
    private static FocusBlunder Blunder(string gameId, int ply, string? side, string? motif, double? swing = 40, string? headline = null) =>
        new(gameId, ply, "Bb4", "fen b - -", "black", "a5c5", "Rxa5", side, motif, headline ?? $"{motif} at ply {ply}", swing);

    private static List<FocusGame> Recent(int count) => Enumerable.Range(0, count).Select(i => Game($"g{i}", i)).ToList();

    [Fact]
    public void Uses_the_last_30_days_when_they_hold_at_least_15_games()
    {
        var games = Recent(20).Concat(Enumerable.Range(0, 10).Select(i => Game($"old{i}", 100 + i))).ToList();

        var focus = WeeklyFocusCalculator.Compute(games, []);

        Assert.Equal("the last 30 days", focus.Window);
        Assert.Equal(20, focus.GameCount);
        Assert.Equal(Latest.AddDays(-19), focus.From);
        Assert.Equal(Latest, focus.To);
    }

    [Fact]
    public void Falls_back_to_the_last_50_games_with_their_real_date_range()
    {
        var games = Enumerable.Range(0, 60).Select(i => Game($"g{i}", i * 10)).ToList();   // one game every 10 days

        var focus = WeeklyFocusCalculator.Compute(games, []);

        Assert.Equal("your last 50 games", focus.Window);
        Assert.Equal(50, focus.GameCount);
        Assert.Equal(Latest.AddDays(-490), focus.From);
        Assert.Equal(Latest, focus.To);
    }

    [Fact]
    public void The_focus_is_the_most_common_named_kind_and_one_with_no_tactic_never_wins()
    {
        var blunders = new List<FocusBlunder>
        {
            Blunder("g0", 3, "positional", null), Blunder("g1", 5, "positional", null), Blunder("g2", 7, "positional", null),
            Blunder("g3", 9, "allowed", "ignored_threat"), Blunder("g4", 11, "allowed", "ignored_threat"),
            Blunder("g5", 13, "allowed", "hung_piece"),
        };

        var focus = WeeklyFocusCalculator.Compute(Recent(20), blunders);

        Assert.Equal(LeakCatalog.IgnoredThreat, focus.Focus);
        Assert.Equal(6, focus.BlunderCount);
        Assert.Equal(3, focus.OtherCount);
        Assert.Equal([("ignored_threat", 2), ("hung_piece", 1)], focus.Top.Select(t => (t.Key, t.Count)));
    }

    [Fact]
    public void Blunders_in_already_decided_positions_are_left_out()
    {
        var blunders = new List<FocusBlunder>
        {
            Blunder("g0", 3, "allowed", "fork", swing: 2), Blunder("g1", 5, "allowed", "fork", swing: 4),   // cost almost nothing
            Blunder("g2", 7, "allowed", "hung_piece", swing: 55),
        };

        var focus = WeeklyFocusCalculator.Compute(Recent(20), blunders);

        Assert.Equal(LeakCatalog.HungPiece, focus.Focus);
        Assert.Equal(1, focus.BlunderCount);
    }

    [Fact]
    public void Unexplained_blunders_are_counted_separately_and_never_chosen()
    {
        var blunders = new List<FocusBlunder>
        {
            Blunder("g0", 3, null, null, swing: null), Blunder("g1", 5, null, null, swing: null),
            Blunder("g2", 7, "missed", "fork"),
        };

        var focus = WeeklyFocusCalculator.Compute(Recent(20), blunders);

        Assert.Equal(2, focus.UnexplainedCount);
        Assert.Equal(1, focus.BlunderCount);
        Assert.Equal(LeakCatalog.MissedWin, focus.Focus);
    }

    [Fact]
    public void With_no_named_tactic_there_is_no_focus()
    {
        var focus = WeeklyFocusCalculator.Compute(Recent(20), [Blunder("g0", 3, "positional", null)]);

        Assert.Null(focus.Focus);
        Assert.Empty(focus.Top);
        Assert.Empty(focus.Examples);
    }

    [Fact]
    public void Examples_are_the_latest_of_the_focus_kind_with_what_happened_and_the_position()
    {
        var blunders = Enumerable.Range(0, 8)
            .Select(i => Blunder($"g{i}", 10 + i, "allowed", "ignored_threat", headline: $"note {i}"))
            .Append(Blunder("g0", 99, "allowed", "hung_piece"))
            .ToList();

        var focus = WeeklyFocusCalculator.Compute(Recent(20), blunders);

        Assert.Equal(WeeklyFocusCalculator.ExampleCount, focus.Examples.Count);
        var first = focus.Examples[0];
        Assert.Equal(Latest, first.Date);   // g0 is the latest game
        Assert.Equal("note 0", first.Headline);
        Assert.Equal(5, first.Move);         // ply 10 is Black's 5th move
        Assert.Equal(("Bb4", "fen b - -", "black", "a5c5", "Rxa5"), (first.PlayedSan, first.Fen, first.Color, first.BestUci, first.BestSan));
        Assert.All(focus.Examples, e => Assert.StartsWith("note", e.Headline));
    }

    [Fact]
    public void The_overall_top_kind_looks_at_every_game_not_just_the_window()
    {
        var games = Recent(20).Concat(Enumerable.Range(0, 30).Select(i => Game($"old{i}", 200 + i))).ToList();
        var blunders = Enumerable.Range(0, 5).Select(i => Blunder($"old{i}", 9, "allowed", "moved_defender"))
            .Append(Blunder("g0", 9, "allowed", "fork"))
            .ToList();

        var focus = WeeklyFocusCalculator.Compute(games, blunders);

        Assert.Equal(LeakCatalog.ForkAllowed, focus.Focus);
        Assert.Equal(LeakCatalog.MovedDefender, focus.OverallTop);
    }

    [Theory]
    [InlineData("allowed", "ignored_threat", "ignored_threat")]
    [InlineData("allowed", "hung_piece", "hung_piece")]
    [InlineData("allowed", "moved_defender", "moved_defender")]
    [InlineData("allowed", "lost_piece", "loose_piece")]
    [InlineData("allowed", "free_piece", "loose_piece")]
    [InlineData("allowed", "fork", "fork")]
    [InlineData("allowed", "skewer", "line")]
    [InlineData("allowed", "discovered_check", "line")]
    [InlineData("allowed", "back_rank_mate", "mate")]
    [InlineData("missed", "smothered_mate", "missed_mate")]
    [InlineData("missed", "pin", "missed_win")]
    [InlineData("positional", null, "other")]
    [InlineData(null, null, "other")]
    public void Each_note_falls_into_one_kind(string? side, string? motif, string kind)
    {
        Assert.Equal(kind, LeakCatalog.For(side, motif).Key);
    }

    [Fact]
    public void No_games_means_no_focus()
    {
        var focus = WeeklyFocusCalculator.Compute([], []);

        Assert.Equal(0, focus.GameCount);
        Assert.Null(focus.Focus);
        Assert.Null(focus.From);
    }
}
