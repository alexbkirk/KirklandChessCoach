using ChessCoach.Core.Domain;
using ChessCoach.Core.Training;

namespace ChessCoach.Core.Tests;

public sealed class LeitnerSchedulerTests
{
    [Fact]
    public void A_new_puzzle_is_due_now_in_box_zero()
    {
        var p = LeitnerScheduler.New("a_1", TestData.Now);

        Assert.Equal(0, p.Box);
        Assert.Equal(TestData.Now, p.DueUtc);
        Assert.Equal(0, p.Tries);
    }

    [Fact]
    public void A_correct_answer_moves_up_one_box_and_schedules_by_that_box_interval()
    {
        var p = LeitnerScheduler.Apply(LeitnerScheduler.New("a_1", TestData.Now), correct: true, TestData.Now);

        Assert.Equal(1, p.Box);
        Assert.Equal(TestData.Now.AddDays(1), p.DueUtc);
        Assert.Equal(1, p.Tries);
        Assert.Equal(1, p.Correct);
        Assert.Equal(TestData.Now, p.LastSeenUtc);
    }

    [Fact]
    public void Intervals_follow_the_box_table_and_the_box_is_capped_at_six()
    {
        var p = LeitnerScheduler.New("a_1", TestData.Now);
        for (var i = 0; i < 10; i++)
        {
            p = LeitnerScheduler.Apply(p, correct: true, TestData.Now);
        }

        Assert.Equal(LeitnerScheduler.MaxBox, p.Box);
        Assert.Equal(TestData.Now.AddDays(60), p.DueUtc);
        Assert.Equal(10, p.Tries);
        Assert.Equal(10, p.Correct);
    }

    [Fact]
    public void A_miss_resets_to_box_zero_and_retries_in_five_minutes()
    {
        var advanced = new PuzzleProgress("a_1", 4, TestData.Now, 4, 4, TestData.Now);

        var p = LeitnerScheduler.Apply(advanced, correct: false, TestData.Now);

        Assert.Equal(0, p.Box);
        Assert.Equal(TestData.Now.AddMinutes(5), p.DueUtc);
        Assert.Equal(5, p.Tries);
        Assert.Equal(4, p.Correct);
    }
}
