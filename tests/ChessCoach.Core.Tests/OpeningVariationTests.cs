using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Domain;
using ChessCoach.Core.Training;

namespace ChessCoach.Core.Tests;

public sealed class OpeningVariationTests
{
    private static readonly OpeningScope Scope = new("black", "Caro Kann Defense", null);

    // Positions are labelled by name rather than real FENs: the calculator only compares them.
    private static VariationPly P(string game, int ply, string san, int cpLoss = 0, string? best = null, string? bestUci = null) =>
        new(game, ply, $"pos-{ply}-{(ply > 3 ? "x" : san)} w KQkq -", san, san.ToLowerInvariant(), ply % 2 == 0, best, bestUci, cpLoss);

    /// <summary>1.e4 c6 2.Nc3 d5 3.Nf3 and then Black's 3rd move: Nf6 (a slip) or dxe4.</summary>
    private static IEnumerable<VariationPly> Game(string id, string third, int loss)
    {
        yield return new(id, 1, "start w KQkq - 0 1", "e4", "e2e4", false, null, null, 0);
        yield return new(id, 2, "after-e4 b KQkq - 0 1", "c6", "c7c6", true, "c6", "c7c6", 0);
        yield return new(id, 3, "after-c6 w KQkq - 0 2", "Nc3", "b1c3", false, null, null, 0);
        yield return new(id, 4, "after-Nc3 b KQkq - 1 2", "d5", "d7d5", true, "d5", "d7d5", 5);
        yield return new(id, 5, "after-d5 w KQkq - 0 3", "Nf3", "g1f3", false, null, null, 0);
        yield return new(id, 6, $"after-Nf3 b KQkq - 1 {id.Length}", third, third switch { "Nf6" => "g8f6", "dxe4" => "d5e4", _ => "e7e6" }, true, "dxe4", "d5e4", loss);
    }

    private static VariationGame G(string id, string result) => new(id, result, "2026-01-01", "opp", $"https://x/{id}");

    [Fact]
    public void The_usual_line_follows_the_most_common_move_while_three_games_agree()
    {
        var plies = Game("a", "Nf6", 120).Concat(Game("b", "Nf6", 90)).Concat(Game("c", "Nf6", 150)).Concat(Game("d", "dxe4", 0)).ToList();

        var review = OpeningVariationCalculator.Compute(Scope, [G("a", "win"), G("b", "loss"), G("c", "loss"), G("d", "draw")], plies);

        Assert.Equal(["e4", "c6", "Nc3", "d5", "Nf3", "Nf6"], review.Line.Select(m => m.San));
        var nf6 = review.Line[^1];
        Assert.True(nf6.IsMine);
        Assert.Equal(3, nf6.Games);
        Assert.Equal(4, nf6.Reached);
        Assert.Equal(120, nf6.AvgCpLoss);
        Assert.Equal(2, nf6.Slips);
        Assert.Equal("dxe4", nf6.BestSan);
        Assert.Equal(1, review.Record.Wins);
        Assert.Equal(1, review.Record.Draws);
        Assert.Equal(2, review.Record.Losses);
    }

    [Fact]
    public void The_line_stops_when_fewer_than_three_games_follow()
    {
        var plies = Game("a", "Nf6", 0).Concat(Game("b", "Nf6", 0)).Concat(Game("c", "dxe4", 0)).ToList();

        var review = OpeningVariationCalculator.Compute(Scope, [G("a", "win"), G("b", "win"), G("c", "win")], plies);

        Assert.Equal(["e4", "c6", "Nc3", "d5", "Nf3"], review.Line.Select(m => m.San));
    }

    [Fact]
    public void A_trouble_spot_groups_one_position_across_games_and_accepts_the_best_and_sound_moves()
    {
        var plies = Game("a", "Nf6", 120).Concat(Game("b", "Nf6", 90)).Concat(Game("c", "Nf6", 150)).Concat(Game("d", "dxe4", 0)).ToList();

        var review = OpeningVariationCalculator.Compute(Scope, [G("a", "win"), G("b", "loss"), G("c", "loss"), G("d", "draw")], plies);

        var spot = Assert.Single(review.Trouble);
        Assert.Equal("after-Nf3 b KQkq -", spot.Key);
        Assert.Equal(6, spot.Ply);
        Assert.Equal("1.e4 c6 2.Nc3 d5 3.Nf3", spot.Path);
        Assert.Equal(4, spot.Reached);
        Assert.Equal(2, spot.Slips);
        Assert.Equal("Nf6", spot.MistakeSan);
        Assert.Equal(2, spot.MistakeCount);       // the 90 cp Nf6 is not a slip
        Assert.Equal(135, spot.MistakeAvgCpLoss);
        Assert.Equal("dxe4", spot.BestSan);
        Assert.Equal(["d5e4"], spot.Accept);
        Assert.Equal("https://x/c", spot.ExampleUrl);   // the costliest slip
        Assert.True(OpeningVariationCalculator.IsCorrect(spot, "d5e4"));
        Assert.False(OpeningVariationCalculator.IsCorrect(spot, "g8f6"));
    }

    [Fact]
    public void A_sound_alternative_you_played_also_counts_as_right()
    {
        var plies = Game("a", "Nf6", 200).Concat(Game("b", "e6", 20)).ToList();

        var spot = Assert.Single(OpeningVariationCalculator.Compute(Scope, [G("a", "loss"), G("b", "win")], plies).Trouble);

        Assert.Equal(["d5e4", "e7e6"], spot.Accept);
    }

    [Fact]
    public void Positions_without_a_slip_are_not_trouble_and_spots_are_ordered_by_slips()
    {
        var plies = new List<VariationPly>
        {
            P("a", 2, "Nf6", 150), P("b", 2, "Nf6", 150), P("c", 2, "Nf6", 0),
            P("a", 4, "Bg4", 300),
            P("a", 6, "O-O", 10),
        };

        var trouble = OpeningVariationCalculator.Compute(Scope, [G("a", "win"), G("b", "win"), G("c", "win")], plies).Trouble;

        Assert.Equal(["Nf6", "Bg4"], trouble.Select(t => t.MistakeSan));
    }

    [Fact]
    public void A_one_off_slip_after_move_ten_is_left_out_but_a_repeated_one_is_kept()
    {
        var plies = new List<VariationPly>
        {
            P("a", 24, "Qh5", 1000),                         // move 12, once: a middlegame tactic
            P("a", 26, "Rd1", 300), P("b", 26, "Rd1", 0),    // move 13, twice: kept
            P("a", 20, "h3", 150),                           // move 10, once: kept
        };

        var trouble = OpeningVariationCalculator.Compute(Scope, [G("a", "win"), G("b", "win")], plies).Trouble;

        Assert.Equal(["Rd1", "h3"], trouble.Select(t => t.MistakeSan).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void The_position_key_drops_the_move_counters()
    {
        Assert.Equal("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq -",
            OpeningVariationCalculator.PositionKey("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1"));
    }

    [Fact]
    public async Task Answering_records_the_attempt_with_the_time()
    {
        var repo = new FakeRepository();
        var clock = new FakeClock(new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.Zero));
        var service = new OpeningVariationService(repo, clock);
        var spot = new TroubleSpot("k", "fen", 6, "path", 4, 2, "Nf6", 3, 120, "dxe4", "d5e4", ["d5e4"], "u");

        Assert.False(await service.AnswerAsync(spot, "g8f6"));
        Assert.True(await service.AnswerAsync(spot, "d5e4"));

        Assert.Equal([("k", "g8f6", false, clock.UtcNow), ("k", "d5e4", true, clock.UtcNow)], repo.Recorded);
    }

    private sealed class FakeRepository : IOpeningRepository
    {
        public List<(string Key, string Uci, bool Correct, DateTimeOffset At)> Recorded { get; } = [];

        public Task<IReadOnlyList<OpeningGame>> LoadAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<(IReadOnlyList<VariationGame> Games, IReadOnlyList<VariationPly> Plies)> LoadVariationAsync(OpeningScope scope, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<string, PracticeTally>> LoadPracticeAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RecordPracticeAsync(string key, string playedUci, bool correct, DateTimeOffset atUtc, CancellationToken cancellationToken = default)
        {
            Recorded.Add((key, playedUci, correct, atUtc));
            return Task.CompletedTask;
        }
    }
}
