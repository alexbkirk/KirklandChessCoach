namespace ChessCoach.Core.Training;

public enum StrikeState
{
    Playing,
    Solved,
    /// <summary>Out of tries (or gave up): the answer is shown and the position counts as a miss.</summary>
    Failed,
}

/// <summary>How much help the position gives at this point.</summary>
public enum StrikeHint
{
    None,

    /// <summary>One try used: "Not that one", with the tries left.</summary>
    Nudge,

    /// <summary>Two tries used: the piece that should move is marked.</summary>
    Piece,

    /// <summary>Out of tries or gave up: the engine's move is shown.</summary>
    Answer,
}

/// <summary>What a finished position came to.</summary>
/// <param name="Solved">The right move was found within the tries.</param>
/// <param name="Strikes">Wrong moves made before the end.</param>
/// <param name="FirstTry">Solved with the first move and no hint: the only result that shows the position is known.</param>
public sealed record PracticeResult(bool Solved, int Strikes, bool FirstTry);

/// <summary>
/// The "three strikes" rule shared by every find-the-move exercise (openings, the focus, puzzles). A wrong move is a strike.
/// Each strike brings more help: a nudge, then the piece to move, then the answer. Three strikes, or giving up, ends the
/// position as a miss; the right move at any point ends it as solved. A position is only a clean success if it was
/// solved with the first move and no hint.
/// </summary>
public sealed class StrikeTracker(int maxStrikes = StrikeTracker.DefaultStrikes)
{
    public const int DefaultStrikes = 3;

    public int MaxStrikes { get; } = maxStrikes;

    /// <summary>Wrong moves so far.</summary>
    public int Strikes { get; private set; }

    /// <summary>Every move tried so far, right or wrong.</summary>
    public int Attempts { get; private set; }

    public bool HintShown { get; private set; }

    public StrikeState State { get; private set; } = StrikeState.Playing;

    public bool IsFinished => State != StrikeState.Playing;

    public int TriesLeft => State == StrikeState.Playing ? MaxStrikes - Strikes : 0;

    public StrikeHint Hint => State switch
    {
        StrikeState.Failed => StrikeHint.Answer,
        StrikeState.Solved => StrikeHint.None,
        _ => Strikes switch { 0 => HintShown ? StrikeHint.Piece : StrikeHint.None, 1 => HintShown ? StrikeHint.Piece : StrikeHint.Nudge, _ => StrikeHint.Piece },
    };

    public PracticeResult? Result => State switch
    {
        StrikeState.Solved => new PracticeResult(true, Strikes, Strikes == 0 && !HintShown),
        StrikeState.Failed => new PracticeResult(false, Strikes, false),
        _ => null,
    };

    /// <summary>Records a move. Does nothing once the position is finished. Returns the state after the move.</summary>
    public StrikeState Register(bool correct)
    {
        if (IsFinished) return State;
        Attempts++;
        if (correct)
        {
            State = StrikeState.Solved;
        }
        else
        {
            Strikes++;
            if (Strikes >= MaxStrikes) State = StrikeState.Failed;
        }

        return State;
    }

    /// <summary>Asks for the piece to move. It makes a first-try success no longer clean.</summary>
    public void ShowHint()
    {
        if (!IsFinished) HintShown = true;
    }

    /// <summary>Shows the answer now; the position counts as a miss.</summary>
    public void GiveUp()
    {
        if (!IsFinished) State = StrikeState.Failed;
    }

    /// <summary>Starts the same position again with all tries back.</summary>
    public void Reset()
    {
        Strikes = 0;
        Attempts = 0;
        HintShown = false;
        State = StrikeState.Playing;
    }
}
