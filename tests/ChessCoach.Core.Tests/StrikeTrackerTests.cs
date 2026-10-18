using ChessCoach.Core.Training;

namespace ChessCoach.Core.Tests;

public sealed class StrikeTrackerTests
{
    [Fact]
    public void The_right_move_first_time_is_a_clean_success()
    {
        var tracker = new StrikeTracker();

        Assert.Equal(StrikeState.Solved, tracker.Register(correct: true));

        Assert.Equal(new PracticeResult(true, 0, FirstTry: true), tracker.Result);
        Assert.True(tracker.IsFinished);
    }

    [Fact]
    public void Each_wrong_move_is_a_strike_and_brings_more_help()
    {
        var tracker = new StrikeTracker();
        Assert.Equal((StrikeHint.None, 3), (tracker.Hint, tracker.TriesLeft));

        tracker.Register(false);
        Assert.Equal((StrikeHint.Nudge, 2), (tracker.Hint, tracker.TriesLeft));

        tracker.Register(false);
        Assert.Equal((StrikeHint.Piece, 1), (tracker.Hint, tracker.TriesLeft));
        Assert.Null(tracker.Result);

        tracker.Register(false);
        Assert.Equal(StrikeState.Failed, tracker.State);
        Assert.Equal((StrikeHint.Answer, 0), (tracker.Hint, tracker.TriesLeft));
        Assert.Equal(new PracticeResult(false, 3, FirstTry: false), tracker.Result);
    }

    [Fact]
    public void Finding_it_on_the_third_try_is_solved_but_not_clean()
    {
        var tracker = new StrikeTracker();
        tracker.Register(false);
        tracker.Register(false);

        tracker.Register(true);

        Assert.Equal(new PracticeResult(true, 2, FirstTry: false), tracker.Result);
        Assert.Equal(StrikeHint.None, tracker.Hint);   // nothing left to help with
    }

    [Fact]
    public void A_hint_makes_a_first_try_success_unclean_and_shows_the_piece_early()
    {
        var tracker = new StrikeTracker();

        tracker.ShowHint();
        Assert.Equal(StrikeHint.Piece, tracker.Hint);
        tracker.Register(true);

        Assert.Equal(new PracticeResult(true, 0, FirstTry: false), tracker.Result);
    }

    [Fact]
    public void Giving_up_shows_the_answer_and_counts_as_a_miss()
    {
        var tracker = new StrikeTracker();

        tracker.GiveUp();

        Assert.Equal(StrikeState.Failed, tracker.State);
        Assert.Equal(StrikeHint.Answer, tracker.Hint);
        Assert.Equal(new PracticeResult(false, 0, FirstTry: false), tracker.Result);
    }

    [Fact]
    public void Once_finished_further_moves_and_hints_change_nothing()
    {
        var tracker = new StrikeTracker();
        tracker.Register(true);

        tracker.Register(false);
        tracker.ShowHint();
        tracker.GiveUp();

        Assert.Equal(StrikeState.Solved, tracker.State);
        Assert.Equal(0, tracker.Strikes);
        Assert.Equal(1, tracker.Attempts);
        Assert.Equal(new PracticeResult(true, 0, FirstTry: true), tracker.Result);
    }

    [Fact]
    public void Reset_gives_all_the_tries_back()
    {
        var tracker = new StrikeTracker();
        tracker.Register(false);
        tracker.ShowHint();
        tracker.GiveUp();

        tracker.Reset();

        Assert.Equal((StrikeState.Playing, 0, 0, StrikeHint.None, 3), (tracker.State, tracker.Strikes, tracker.Attempts, tracker.Hint, tracker.TriesLeft));
        Assert.Null(tracker.Result);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(5, 5)]
    public void The_number_of_strikes_can_be_changed(int strikes, int movesToFail)
    {
        var tracker = new StrikeTracker(strikes);

        for (var i = 0; i < movesToFail - 1; i++) tracker.Register(false);
        Assert.Equal(StrikeState.Playing, tracker.State);
        tracker.Register(false);

        Assert.Equal(StrikeState.Failed, tracker.State);
    }
}
