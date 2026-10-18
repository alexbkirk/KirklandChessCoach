using System.Globalization;
using ChessCoach.Core.Stats;

namespace ChessCoach.Core.Review;

public sealed record ReviewSummary(int Inaccuracies, int Mistakes, int Blunders, double AverageLoss);

/// <summary>One line of the move list: a move number with White's and Black's move, either of which may be missing.</summary>
public sealed record MoveRow(int Number, ReviewMove? White, ReviewMove? Black);

/// <summary>Pure helpers for the review page: labels, the evaluation curve, key moments and the annotation symbols.</summary>
public static class ReviewTimeline
{
    /// <summary>The symbols a player may put on a move, best first.</summary>
    public static readonly IReadOnlyList<string> Glyphs = ["!!", "!", "!?", "?!", "?", "??"];

    /// <summary>The longest note kept for a move or a game.</summary>
    public const int MaxNoteLength = 5000;

    /// <summary>Evaluations of this size or more are mate scores.</summary>
    private const int MateThreshold = 9000;

    public static bool IsValidGlyph(string? glyph) => glyph is null or "" || Glyphs.Contains(glyph);

    /// <summary>"12. Nf3" for White's 12th move, "12... Nf6" for Black's.</summary>
    public static string MoveLabel(int ply, string san) =>
        ply % 2 == 1
            ? $"{(ply + 1) / 2}. {san}"
            : $"{ply / 2}... {san}";

    /// <summary>Evaluation as pawns with a sign ("+0.35", "-1.20"), or "Mate" when it is a mate score.</summary>
    public static string FormatEval(int whiteCentipawns)
    {
        if (Math.Abs(whiteCentipawns) >= MateThreshold) return whiteCentipawns > 0 ? "White mates" : "Black mates";
        var pawns = whiteCentipawns / 100.0;
        return (pawns >= 0 ? "+" : "-") + Math.Abs(pawns).ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// White's winning chances (0 to 100) in every position of the game: the start, then after each move. One more entry than moves.
    /// </summary>
    public static IReadOnlyList<double> WhiteChances(IReadOnlyList<ReviewMove> moves)
    {
        if (moves.Count == 0) return [];
        var chances = new List<double>(moves.Count + 1) { WinChances.Percent(moves[0].EvalBefore) };
        chances.AddRange(moves.Select(m => WinChances.Percent(m.EvalAfter)));
        return chances;
    }

    /// <summary>Counts the player's own inaccuracies, mistakes and blunders, and their average loss (the last move of the game is left out, as in the stats).</summary>
    public static ReviewSummary Summarise(IReadOnlyList<ReviewMove> moves)
    {
        var mine = moves.Where(m => m.IsMine && !m.IsLast).ToList();
        return new ReviewSummary(
            Inaccuracies: mine.Count(m => m.Class == "inaccuracy"),
            Mistakes: mine.Count(m => m.Class == "mistake"),
            Blunders: mine.Count(m => m.Class == "blunder"),
            AverageLoss: mine.Count == 0 ? 0 : mine.Average(m => (double)m.CpLoss));
    }

    /// <summary>The plies of the player's own mistakes and blunders (not on the last move of the game).</summary>
    public static IReadOnlyList<int> KeyMoves(IReadOnlyList<ReviewMove> moves) =>
        moves.Where(m => m.IsMine && !m.IsLast && m.Class is "mistake" or "blunder").Select(m => m.Ply).ToList();

    /// <summary>
    /// The position to jump to for the next (or previous) of the player's own mistakes or blunders, after <paramref name="position"/>
    /// (0 is the start, n is after the nth move). Null when there is none that way.
    /// </summary>
    public static int? KeyMoment(IReadOnlyList<ReviewMove> moves, int position, bool forward)
    {
        // landing on the position before the move shows the player what they faced
        var candidates = KeyMoves(moves).Select(ply => ply - 1);
        return forward
            ? candidates.Where(p => p > position).Select(p => (int?)p).FirstOrDefault()
            : candidates.Where(p => p < position).Select(p => (int?)p).LastOrDefault();
    }

    /// <summary>
    /// The move list as numbered lines. A game that starts with Black to move, or has a gap in its plies, still lists every move:
    /// the missing side of a line is null.
    /// </summary>
    public static IReadOnlyList<MoveRow> MoveRows(IReadOnlyList<ReviewMove> moves)
    {
        if (moves.Count == 0) return [];
        var byPly = moves.GroupBy(m => m.Ply).ToDictionary(g => g.Key, g => g.First());
        var rows = new List<MoveRow>();
        for (var number = (moves.Min(m => m.Ply) + 1) / 2; number <= (moves.Max(m => m.Ply) + 1) / 2; number++)
        {
            var white = byPly.GetValueOrDefault(number * 2 - 1);
            var black = byPly.GetValueOrDefault(number * 2);
            if (white is not null || black is not null) rows.Add(new MoveRow(number, white, black));
        }

        return rows;
    }

    /// <summary>The note cut to about <paramref name="maxCharacters"/> characters with an ellipsis, never in the middle of a character such as an emoji.</summary>
    public static string Shorten(string note, int maxCharacters)
    {
        var characters = new StringInfo(note);
        return characters.LengthInTextElements <= maxCharacters
            ? note
            : characters.SubstringByTextElements(0, maxCharacters) + "…";
    }
}
