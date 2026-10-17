using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Domain;
using ChessCoach.Core.Training;

namespace ChessCoach.Core.Tests;

public sealed class PuzzleServiceTests
{
    private sealed class FakeRepository(IReadOnlyList<Puzzle> pool) : IPuzzleRepository
    {
        public Dictionary<string, PuzzleProgress> Progress { get; } = [];

        public List<(string Uci, bool Correct, bool GaveUp, bool Hint)> Attempts { get; } = [];

        public Task<IReadOnlyList<Puzzle>> LoadPoolAsync(CancellationToken cancellationToken = default) => Task.FromResult(pool);

        public Task<IReadOnlyDictionary<string, PuzzleProgress>> LoadProgressAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, PuzzleProgress>>(new Dictionary<string, PuzzleProgress>(Progress));

        public Task RecordAttemptAsync(PuzzleProgress progress, string playedUci, bool correct, bool gaveUp, bool hintUsed, DateTimeOffset atUtc, CancellationToken cancellationToken = default)
        {
            Progress[progress.PositionId] = progress;
            Attempts.Add((playedUci, correct, gaveUp, hintUsed));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task A_correct_answer_is_graded_recorded_and_scheduled()
    {
        var puzzle = TestData.Puzzle("g_1", 300, "g5d8");
        var repo = new FakeRepository([puzzle]);
        var service = new PuzzleService(repo, new FakeClock(TestData.Now));

        var correct = await service.AnswerAsync(puzzle, "g5d8", gaveUp: false, hintUsed: false);

        Assert.True(correct);
        Assert.Equal(1, repo.Progress["g_1"].Box);
        Assert.Equal(TestData.Now.AddDays(1), repo.Progress["g_1"].DueUtc);
        Assert.Equal(("g5d8", true, false, false), repo.Attempts.Single());
    }

    [Fact]
    public async Task Showing_the_answer_counts_as_a_miss_even_when_hinted()
    {
        var puzzle = TestData.Puzzle("g_1", 300, "g5d8");
        var repo = new FakeRepository([puzzle]);
        var service = new PuzzleService(repo, new FakeClock(TestData.Now));

        var correct = await service.AnswerAsync(puzzle, null, gaveUp: true, hintUsed: true);

        Assert.False(correct);
        Assert.Equal(0, repo.Progress["g_1"].Box);
        Assert.Equal(("", false, true, true), repo.Attempts.Single());
    }
}
