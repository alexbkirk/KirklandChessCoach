using ChessCoach.Core.Domain;
using ChessCoach.Core.Training;

namespace ChessCoach.Core.Tests;

public sealed class PuzzleSetTests
{
    private static PuzzleProgress Progress(string id, int box, TimeSpan dueFromNow) =>
        new(id, box, TestData.Now + dueFromNow, 1, 0, TestData.Now);

    [Fact]
    public void Due_reviews_come_first_lowest_box_first_then_unseen_costliest_first_then_not_yet_due()
    {
        var pool = new[]
        {
            TestData.Puzzle("fresh_big", 900),
            TestData.Puzzle("fresh_small", 100),
            TestData.Puzzle("due_box2", 50),
            TestData.Puzzle("due_box0", 50),
            TestData.Puzzle("later_soon", 50),
        };
        var progress = new Dictionary<string, PuzzleProgress>
        {
            ["due_box2"] = Progress("due_box2", 2, -TimeSpan.FromHours(1)),
            ["due_box0"] = Progress("due_box0", 0, -TimeSpan.FromMinutes(1)),
            ["later_soon"] = Progress("later_soon", 1, TimeSpan.FromDays(1)),
        };

        var set = PuzzleSet.Pick(pool, progress, TestData.Now, size: 5);

        Assert.Equal(["due_box0", "due_box2", "fresh_big", "fresh_small", "later_soon"], set.Select(p => p.PositionId));
    }

    [Fact]
    public void A_set_is_capped_at_the_requested_size()
    {
        var pool = Enumerable.Range(1, 12).Select(i => TestData.Puzzle($"p{i}", 1200 - 100 * i)).ToList();

        var set = PuzzleSet.Pick(pool, new Dictionary<string, PuzzleProgress>(), TestData.Now);

        Assert.Equal(PuzzleSet.DefaultSize, set.Count);
        Assert.Equal("p1", set[0].PositionId);   // unseen puzzles keep the caller's order (costliest first)
    }

    [Fact]
    public void An_empty_pool_gives_an_empty_set()
    {
        Assert.Empty(PuzzleSet.Pick([], new Dictionary<string, PuzzleProgress>(), TestData.Now));
    }
}
