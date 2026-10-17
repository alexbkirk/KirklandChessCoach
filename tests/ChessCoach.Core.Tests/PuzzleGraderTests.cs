using ChessCoach.Core.Training;

namespace ChessCoach.Core.Tests;

public sealed class PuzzleGraderTests
{
    [Fact]
    public void Any_accepted_move_is_correct_and_others_are_not()
    {
        string[] accept = ["g5d8", "e2e4"];

        Assert.True(PuzzleGrader.IsCorrect("g5d8", accept));
        Assert.True(PuzzleGrader.IsCorrect("E2E4", accept));
        Assert.False(PuzzleGrader.IsCorrect("d2d4", accept));
    }
}
