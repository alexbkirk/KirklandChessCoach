using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Review;

namespace ChessCoach.Core.Tests;

public sealed class ReviewTimelineTests
{
    private static ReviewMove Move(int ply, string cls = "good", bool mine = true, bool last = false, int before = 0, int after = 0, int loss = 0) =>
        new(ply, ply % 2 == 1 ? "white" : "black", mine, last, "e4", "e2e4", "fen", null, null, before, after, loss, cls, null);

    [Theory]
    [InlineData(1, "e4", "1. e4")]
    [InlineData(2, "e5", "1... e5")]
    [InlineData(23, "Nf3", "12. Nf3")]
    [InlineData(24, "Nf6", "12... Nf6")]
    public void Moves_are_labelled_with_their_move_number_and_side(int ply, string san, string expected)
    {
        Assert.Equal(expected, ReviewTimeline.MoveLabel(ply, san));
    }

    [Theory]
    [InlineData(35, "+0.35")]
    [InlineData(0, "+0.00")]
    [InlineData(-120, "-1.20")]
    [InlineData(10000, "White mates")]
    [InlineData(-9999, "Black mates")]
    public void Evaluations_read_as_pawns_or_mate(int centipawns, string expected)
    {
        Assert.Equal(expected, ReviewTimeline.FormatEval(centipawns));
    }

    [Fact]
    public void The_curve_has_one_more_point_than_there_are_moves()
    {
        var moves = new[] { Move(1, before: 20, after: 300), Move(2, before: 300, after: -300) };

        var chances = ReviewTimeline.WhiteChances(moves);

        Assert.Equal(3, chances.Count);
        Assert.True(chances[1] > 50);
        Assert.True(chances[2] < 50);
    }

    [Fact]
    public void A_game_without_moves_has_no_curve()
    {
        Assert.Empty(ReviewTimeline.WhiteChances([]));
    }

    [Fact]
    public void The_summary_counts_only_your_own_graded_moves_and_leaves_out_the_last()
    {
        var moves = new[]
        {
            Move(1, "inaccuracy", loss: 50),
            Move(2, "blunder", mine: false, loss: 400),
            Move(3, "mistake", loss: 150),
            Move(5, "blunder", loss: 500),
            Move(7, "blunder", last: true, loss: 900),
        };

        var summary = ReviewTimeline.Summarise(moves);

        Assert.Equal(new ReviewSummary(1, 1, 1, 233.33333333333334), summary);
    }

    [Fact]
    public void Key_moments_are_the_positions_before_your_mistakes_and_blunders()
    {
        // your mistake at ply 3 and blunder at ply 7: the positions before them are 2 and 6
        var moves = new[] { Move(1), Move(2, "blunder", mine: false), Move(3, "mistake"), Move(5, "inaccuracy"), Move(7, "blunder") };

        Assert.Equal(2, ReviewTimeline.KeyMoment(moves, 0, forward: true));
        Assert.Equal(6, ReviewTimeline.KeyMoment(moves, 2, forward: true));
        Assert.Null(ReviewTimeline.KeyMoment(moves, 6, forward: true));
        Assert.Equal(2, ReviewTimeline.KeyMoment(moves, 6, forward: false));
        Assert.Null(ReviewTimeline.KeyMoment(moves, 2, forward: false));
    }

    [Fact]
    public void A_blunder_on_the_last_move_is_not_a_key_moment()
    {
        var moves = new[] { Move(1), Move(3, "blunder", last: true) };

        Assert.Null(ReviewTimeline.KeyMoment(moves, 0, forward: true));
    }

    [Theory]
    [InlineData("!!", true)]
    [InlineData("!", true)]
    [InlineData("!?", true)]
    [InlineData("?!", true)]
    [InlineData("?", true)]
    [InlineData("??", true)]
    [InlineData("", true)]
    [InlineData(null, true)]
    [InlineData("!!!", false)]
    [InlineData("good", false)]
    [InlineData("<b>", false)]
    public void Only_the_six_move_symbols_are_valid(string? glyph, bool valid)
    {
        Assert.Equal(valid, ReviewTimeline.IsValidGlyph(glyph));
    }
}

public sealed class GameReviewServiceTests
{
    private sealed class FakeRepository : IGameReviewRepository
    {
        public List<(string Game, int Ply, string? Glyph, string Note)> Moves { get; } = [];

        public List<(string Game, string Note)> Games { get; } = [];

        public Task<GameReview?> LoadAsync(string gameId, CancellationToken cancellationToken = default) => Task.FromResult<GameReview?>(null);

        public Task SaveMoveAnnotationAsync(string gameId, int ply, string? glyph, string note, CancellationToken cancellationToken = default)
        {
            Moves.Add((gameId, ply, glyph, note));
            return Task.CompletedTask;
        }

        public Task SaveGameNoteAsync(string gameId, string note, CancellationToken cancellationToken = default)
        {
            Games.Add((gameId, note));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task A_move_note_is_trimmed_and_an_empty_symbol_becomes_none()
    {
        var repository = new FakeRepository();

        await new GameReviewService(repository).SaveMoveAnnotationAsync("g1", 12, "", "  I missed the fork.  ");

        Assert.Equal(("g1", 12, (string?)null, "I missed the fork."), repository.Moves.Single());
    }

    [Fact]
    public async Task A_missing_note_is_saved_as_empty_text()
    {
        var repository = new FakeRepository();

        await new GameReviewService(repository).SaveMoveAnnotationAsync("g1", 3, "?!", null);

        Assert.Equal(("g1", 3, "?!", ""), repository.Moves.Single());
    }

    [Fact]
    public async Task A_symbol_that_is_not_one_of_the_six_is_refused_and_nothing_is_saved()
    {
        var repository = new FakeRepository();
        var service = new GameReviewService(repository);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveMoveAnnotationAsync("g1", 3, "<script>", "x"));

        Assert.Empty(repository.Moves);
    }

    [Fact]
    public async Task A_move_number_below_one_is_refused()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new GameReviewService(new FakeRepository()).SaveMoveAnnotationAsync("g1", 0, null, "x"));
    }

    [Fact]
    public async Task A_note_that_is_too_long_is_refused_not_cut()
    {
        var repository = new FakeRepository();
        var service = new GameReviewService(repository);
        var tooLong = new string('x', ReviewTimeline.MaxNoteLength + 1);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveGameNoteAsync("g1", tooLong));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveMoveAnnotationAsync("g1", 1, null, tooLong));

        Assert.Empty(repository.Games);
        Assert.Empty(repository.Moves);
    }

    [Fact]
    public async Task A_note_of_exactly_the_longest_length_is_kept_whole()
    {
        var repository = new FakeRepository();
        var longest = new string('x', ReviewTimeline.MaxNoteLength);

        await new GameReviewService(repository).SaveGameNoteAsync("g1", longest);

        Assert.Equal(longest, repository.Games.Single().Note);
    }
}
