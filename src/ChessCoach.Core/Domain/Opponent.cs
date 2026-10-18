using ChessCoach.Core.Stats;

namespace ChessCoach.Core.Domain;

/// <summary>One of your games against the opponent. Ratings are at the time of the game.</summary>
public sealed record OpponentGame(
    string GameId,
    string Date,
    string Time,
    string Color,
    string Result,
    string Ending,
    int MyRating,
    int OppRating,
    string Family,
    string Opening,
    string Url,
    string Opponent);

/// <summary>A move (either side) from a game against the opponent. <paramref name="Fen"/> is the position before it.</summary>
/// <param name="Clock">Seconds left on the mover's clock after the move, when known.</param>
/// <param name="EvalBefore">Centipawns from White's side before the move, when known.</param>
/// <param name="EvalAfter">Centipawns from White's side after the move, when known.</param>
/// <param name="NoteHeadline">The drill explanation's headline for your blunders that have one.</param>
public sealed record OpponentPly(
    string GameId,
    int Ply,
    bool IsMine,
    string San,
    string Uci,
    string Fen,
    string? BestSan,
    string? BestUci,
    int CpLoss,
    string Class,
    bool IsLast,
    double? Clock,
    string? NoteHeadline,
    int? EvalBefore = null,
    int? EvalAfter = null)
{
    /// <summary>
    /// Winning chances (0 to 100) for the mover before and after the move, when the evaluations are known.
    /// The mover is read from the position's side to move, so games from a custom start position are right too.
    /// </summary>
    public (double Before, double After)? Chances
    {
        get
        {
            if (EvalBefore is null || EvalAfter is null) return null;
            var white = !Fen.Contains(" b ", StringComparison.Ordinal);
            return (WinChances.For(EvalBefore.Value, white), WinChances.For(EvalAfter.Value, white));
        }
    }
}

/// <summary>Average loss and blunder rate for one side in one phase, over a set of games.</summary>
/// <param name="Moves">Moves counted (final moves of games are left out).</param>
/// <param name="AvgCpLoss">Average centipawns lost per move.</param>
/// <param name="BlunderPercent">Share of moves that were blunders (200+ centipawns).</param>
public sealed record PhaseRate(int Moves, double AvgCpLoss, double BlunderPercent)
{
    public static PhaseRate Empty { get; } = new(0, 0, 0);
}

/// <summary>One phase: you and them in these games, and you and your opponents across all your games.</summary>
public sealed record PhaseComparison(string Phase, PhaseRate You, PhaseRate Them, PhaseRate YouUsual, PhaseRate OpponentsUsual);

/// <summary>Baseline rates over all your games, per phase name, for you and for your opponents.</summary>
public sealed record PhaseBaseline(IReadOnlyDictionary<string, PhaseRate> You, IReadOnlyDictionary<string, PhaseRate> Opponents);

/// <summary>A game row on the opponent page.</summary>
public sealed record OpponentGameRow(OpponentGame Game, int MyBlunders, int TheirBlunders);

/// <summary>A blunder (yours or theirs) on the opponent page.</summary>
/// <param name="Before">The mover's winning chances before the move (0 to 100), or null when unknown.</param>
/// <param name="After">The mover's winning chances after the move, or null when unknown.</param>
/// <param name="Reply">The next move by the other side, for their blunders: whether you punished it.</param>
/// <param name="Punished">For their blunders: your reply was the best move or lost less than 100 centipawns, so you kept the gain.</param>
public sealed record OpponentMoment(
    OpponentGame Game,
    int Ply,
    string San,
    string Fen,
    string? BestSan,
    int CpLoss,
    double? Clock,
    string? NoteHeadline,
    double? Before = null,
    double? After = null,
    string? Reply = null,
    string? ReplyBestSan = null,
    bool Punished = false);

/// <summary>Your record against the opponent with one opening family, as one colour.</summary>
public sealed record OpponentOpening(string Family, string Color, Record Record);

public sealed record OpponentReport(
    string Name,
    Record Record,
    string FirstDate,
    string LastDate,
    double AvgTheirRating,
    double AvgRatingGap,
    IReadOnlyList<OpponentGameRow> Games,
    IReadOnlyList<PhaseComparison> Phases,
    IReadOnlyList<OpponentMoment> YourBlunders,
    IReadOnlyList<OpponentMoment> TheirBlunders,
    IReadOnlyList<TroubleSpot> YourOpeningSlips,
    IReadOnlyList<TroubleSpot> TheirOpeningSlips,
    IReadOnlyList<OpponentOpening> Openings,
    IReadOnlyList<RecordSplit> Endings);

/// <summary>A row of the opponents list on the Study page.</summary>
public sealed record OpponentRow(string Name, Record Record, string LastDate, double AvgTheirRating);
