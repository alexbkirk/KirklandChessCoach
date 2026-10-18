using ChessCoach.Core.Domain;
using ChessCoach.Core.Training;

namespace ChessCoach.Core.Tests;

public sealed class DrillFeedbackTests
{
    private static DrillAnswer Answer(string[] extra, string[] missing) =>
        new(extra.Length == 0 && missing.Length == 0, ["e4"], missing, extra);

    [Fact]
    public void The_first_hint_gives_counts_only_and_never_the_squares()
    {
        var feedback = DrillGrader.Feedback(Answer(["h1"], ["d5", "c3"]), StrikeHint.Nudge, triesLeft: 2);

        Assert.Equal("Not quite. 1 of the squares you marked is wrong and you are missing 2. 2 tries left.", feedback.Text);
        Assert.Empty(feedback.WrongSquares);
    }

    [Fact]
    public void The_second_hint_flags_the_wrong_squares_but_still_not_the_missing_ones()
    {
        var feedback = DrillGrader.Feedback(Answer(["h1", "a2"], ["d5"]), StrikeHint.Piece, triesLeft: 1);

        Assert.Equal("Not quite. 2 of the squares you marked are wrong and you are missing 1. The wrong ones are marked red. 1 try left.", feedback.Text);
        Assert.Equal(["h1", "a2"], feedback.WrongSquares);
        Assert.DoesNotContain("d5", feedback.Text);
    }

    [Fact]
    public void Out_of_tries_reveals_the_answer()
    {
        var feedback = DrillGrader.Feedback(Answer(["h1"], ["d5"]), StrikeHint.Answer, triesLeft: 0);

        Assert.StartsWith("Out of tries.", feedback.Text);
        Assert.Equal(["h1"], feedback.WrongSquares);
        Assert.DoesNotContain("tries left", feedback.Text);
    }

    [Theory]
    [InlineData(0, 1, "Not quite. You are missing 1. 2 tries left.")]
    [InlineData(0, 3, "Not quite. You are missing 3. 2 tries left.")]
    [InlineData(1, 0, "Not quite. 1 of the squares you marked is wrong. 2 tries left.")]
    [InlineData(4, 0, "Not quite. 4 of the squares you marked are wrong. 2 tries left.")]
    public void The_counts_read_naturally_when_only_one_kind_of_mistake_is_made(int extra, int missing, string expected)
    {
        var answer = Answer(Enumerable.Range(0, extra).Select(i => $"x{i}").ToArray(), Enumerable.Range(0, missing).Select(i => $"m{i}").ToArray());

        Assert.Equal(expected, DrillGrader.Feedback(answer, StrikeHint.Nudge, triesLeft: 2).Text);
    }

    [Fact]
    public void One_try_left_reads_in_the_singular()
    {
        var feedback = DrillGrader.Feedback(Answer([], ["d5"]), StrikeHint.Nudge, triesLeft: 1);

        Assert.EndsWith("1 try left.", feedback.Text);
    }
}
