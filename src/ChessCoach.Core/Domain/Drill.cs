namespace ChessCoach.Core.Domain;

/// <summary>The three daily-drill formats.</summary>
public enum DrillFormat
{
    /// <summary>After your real (losing) move, mark every piece of yours that can now be taken for free.</summary>
    Hang,

    /// <summary>Three rounds on the position before your move: checks, captures, then threats against you.</summary>
    Cct,

    /// <summary>Judge a move from your games: safe or a blunder?</summary>
    Safe,
}

/// <summary>A position from one of your games used by the drill.</summary>
/// <param name="IsBlunder">True when the move was a blunder (one-move material loss); false for a sound move.</param>
/// <param name="EvalBefore">Centipawns from White's point of view, as stored by the analysis.</param>
public sealed record DrillPosition(
    string PositionId,
    string Fen,
    string Color,
    string Uci,
    string San,
    string BestSan,
    int CpLoss,
    int EvalBefore,
    int EvalAfter,
    bool IsBlunder,
    string Date,
    string Opponent,
    string Url);

/// <summary>The result of one submission: which squares were right, missed or wrongly marked.</summary>
public sealed record DrillAnswer(bool Ok, IReadOnlyList<string> Right, IReadOnlyList<string> Missing, IReadOnlyList<string> Extra);

/// <summary>One of the day's three drill slots and its progress.</summary>
public sealed class DrillSlot
{
    public required int Index { get; init; }

    public required DrillFormat Format { get; init; }

    public required DrillPosition Position { get; init; }

    /// <summary>For <see cref="DrillFormat.Cct"/>: the next round (0 checks, 1 captures, 2 threats).</summary>
    public int Round { get; set; }

    public List<DrillAnswer> Answers { get; init; } = [];

    /// <summary>For <see cref="DrillFormat.Safe"/>: "safe" or "blunder".</summary>
    public string? Choice { get; set; }

    public bool Done { get; set; }

    /// <summary>1 or 0 for Hang and Safe; the number of correct rounds (0 to 3) for Cct.</summary>
    public int Score { get; set; }
}

public sealed record DrillDay(DateOnly Day, IReadOnlyList<DrillSlot> Slots, int Streak, int DaysCompleted);
