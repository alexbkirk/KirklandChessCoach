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

    /// <summary>Safe-or-blunder: right when the user calls a blunder a blunder and a sound move safe.</summary>
    public static bool JudgedCorrectly(bool isBlunder, bool choseSafe) => choseSafe != isBlunder;

    /// <summary>A Cct slot counts as passed with at least two of its three rounds right.</summary>
    public static bool Passed(DrillSlot slot) => slot.Format == DrillFormat.Cct ? slot.Score >= 2 : slot.Score == 1;
}
