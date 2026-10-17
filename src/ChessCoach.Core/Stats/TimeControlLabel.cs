using System.Globalization;
using System.Text.RegularExpressions;

namespace ChessCoach.Core.Stats;

/// <summary>Readable names for chess.com time controls: "600" becomes "10 min", "900+10" becomes "15|10".</summary>
public static partial class TimeControlLabel
{
    public static string From(string timeControl)
    {
        var match = Pattern().Match(timeControl);
        if (!match.Success) return timeControl;   // daily games ("1/259200") keep their raw form
        var minutes = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) / 60.0;
        var text = minutes.ToString("0.##", CultureInfo.InvariantCulture);
        return match.Groups[2].Success ? $"{text}|{match.Groups[2].Value}" : $"{text} min";
    }

    [GeneratedRegex(@"^(\d+)(?:\+(\d+))?$")]
    private static partial Regex Pattern();
}
