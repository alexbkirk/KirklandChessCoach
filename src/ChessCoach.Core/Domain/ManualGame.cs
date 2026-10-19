using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ChessCoach.Core.Domain;

/// <summary>
/// A game played over the board and entered by hand. MyColor is "white" or "black"; Result is "win", "draw" or "loss" from my side.
/// Sans are the moves in SAN order (ply 1 first).
/// </summary>
public sealed record ManualGame(
    DateOnly Date,
    string MyColor,
    string Opponent,
    int MyRating,
    int OppRating,
    string Result,
    string Event,
    IReadOnlyList<string> Sans)
{
    /// <summary>Stable id: "otb-" plus the first 12 lowercase hex chars of a SHA-256 over the other fields.</summary>
    public string Id
    {
        get
        {
            var key = string.Join(
                "|",
                Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                MyColor,
                Opponent,
                MyRating.ToString(CultureInfo.InvariantCulture),
                OppRating.ToString(CultureInfo.InvariantCulture),
                Result,
                Event,
                Moves);
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
            return "otb-" + Convert.ToHexString(hash, 0, 6).ToLowerInvariant();
        }
    }

    /// <summary>The movetext without headers or move numbers, as the games.moves column stores it: "e4 e5 Nf3 ...".</summary>
    public string Moves => string.Join(" ", Sans);

    /// <summary>A standard PGN: seven-tag header, a blank line, then numbered SAN movetext ending with the result.</summary>
    public string Pgn
    {
        get
        {
            var white = IsWhite;
            var headers = new[]
            {
                Tag("Event", string.IsNullOrWhiteSpace(Event) ? "?" : Event),
                Tag("White", white ? "Me" : OrUnknown(Opponent)),
                Tag("Black", white ? OrUnknown(Opponent) : "Me"),
                Tag("Result", ResultToken),
                Tag("Date", Date.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture)),
                Tag("WhiteElo", (white ? MyRating : OppRating).ToString(CultureInfo.InvariantCulture)),
                Tag("BlackElo", (white ? OppRating : MyRating).ToString(CultureInfo.InvariantCulture)),
            };

            var tokens = new List<string>(Sans.Count + 1);
            for (var i = 0; i < Sans.Count; i++)
            {
                tokens.Add(i % 2 == 0 ? $"{i / 2 + 1}. {Sans[i]}" : Sans[i]);
            }
            tokens.Add(ResultToken);

            return string.Join("\n", headers) + "\n\n" + string.Join(" ", tokens);
        }
    }

    private bool IsWhite => MyColor switch
    {
        "white" => true,
        "black" => false,
        _ => throw new InvalidOperationException($"MyColor must be \"white\" or \"black\", not \"{MyColor}\"."),
    };

    private string ResultToken => Result switch
    {
        "draw" => "1/2-1/2",
        "win" => IsWhite ? "1-0" : "0-1",
        "loss" => IsWhite ? "0-1" : "1-0",
        _ => throw new InvalidOperationException($"Result must be \"win\", \"draw\" or \"loss\", not \"{Result}\"."),
    };

    private static string OrUnknown(string name) => string.IsNullOrWhiteSpace(name) ? "?" : name;

    private static string Tag(string name, string value) =>
        $"[{name} \"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"]";
}
