using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Training;

public static class DrillGrader
{
    /// <summary>The rounds of the checks/captures/threats format, in order.</summary>
    public static readonly string[] CctRounds = ["Checks", "Captures", "Threats against you"];

    /// <summary>Compares the squares the user marked with the expected set. Correct only when they match exactly.</summary>
    public static DrillAnswer Grade(IEnumerable<string> selected, IEnumerable<string> expected)
    {
        var chosen = selected.Select(s => s.ToLowerInvariant()).ToHashSet();
        var wanted = expected.Select(s => s.ToLowerInvariant()).ToHashSet();
        var right = chosen.Where(wanted.Contains).Order(StringComparer.Ordinal).ToList();
        var extra = chosen.Where(s => !wanted.Contains(s)).Order(StringComparer.Ordinal).ToList();
        var missing = wanted.Where(s => !chosen.Contains(s)).Order(StringComparer.Ordinal).ToList();
        return new DrillAnswer(extra.Count == 0 && missing.Count == 0, right, missing, extra);
    }

    /// <summary>What to tell the player after a wrong try, and which of their marked squares to flag as wrong.</summary>
    /// <param name="WrongSquares">Squares the player marked that should not be marked; empty until the hint is strong enough to show them.</param>
    public sealed record SquareFeedback(string Text, IReadOnlyList<string> WrongSquares);

    /// <summary>
    /// Feedback for a wrong try at "mark every square" (hanging pieces, checks, captures, threats), growing with the strikes:
    /// first only how many are wrong or missing, then which of the marked squares are wrong, then the full answer.
    /// </summary>
    public static SquareFeedback Feedback(DrillAnswer answer, StrikeHint hint, int triesLeft)
    {
        var tries = triesLeft <= 0 ? "" : $" {triesLeft} {(triesLeft == 1 ? "try" : "tries")} left.";
        var counts = (answer.Extra.Count, answer.Missing.Count) switch
        {
            (0, var missing) => $"You are missing {missing}.",
            (var extra, 0) => $"{extra} of the squares you marked {(extra == 1 ? "is" : "are")} wrong.",
            (var extra, var missing) => $"{extra} of the squares you marked {(extra == 1 ? "is" : "are")} wrong and you are missing {missing}.",
        };

        return hint switch
        {
            StrikeHint.Answer => new SquareFeedback("Out of tries. The full answer is on the board: green is right, yellow was missing, red was wrong.", answer.Extra),
            StrikeHint.Piece => new SquareFeedback($"Not quite. {counts} The wrong ones are marked red.{tries}", answer.Extra),
            _ => new SquareFeedback($"Not quite. {counts}{tries}", []),
        };
    }

    /// <summary>Safe-or-blunder: right when the user calls a blunder a blunder and a sound move safe.</summary>
    public static bool JudgedCorrectly(bool isBlunder, bool choseSafe) => choseSafe != isBlunder;

    /// <summary>A Cct slot counts as passed with at least two of its three rounds right.</summary>
    public static bool Passed(DrillSlot slot) => slot.Format == DrillFormat.Cct ? slot.Score >= 2 : slot.Score == 1;
}
