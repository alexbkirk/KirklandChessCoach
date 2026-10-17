using System.Globalization;
using System.Text;
using ChessCoach.Core.Domain;
using ChessCoach.Core.Stats;

namespace ChessCoach.Core.Training;

/// <summary>
/// Builds the variation page from your games' first 15 moves: your usual line (the most common move at each point,
/// while at least <see cref="MinLineGames"/> games follow it) and the positions where your move lost 100 centipawns or more.
/// A position you have had only once counts only within your first <see cref="SingleSlipMaxMove"/> moves: later one-off
/// slips are middlegame tactics, not opening knowledge.
/// </summary>
public static class OpeningVariationCalculator
{
    public const int SlipCp = 100;
    public const int AcceptCp = 30;
    public const int MinLineGames = 3;
    public const int MaxTrouble = 15;
    public const int SingleSlipMaxMove = 10;

    public static VariationReview Compute(OpeningScope scope, IReadOnlyList<VariationGame> games, IReadOnlyList<VariationPly> plies)
    {
        var record = new Record(
            games.Count(g => g.Result == "win"),
            games.Count(g => g.Result == "draw"),
            games.Count(g => g.Result == "loss"));
        var byGame = plies.GroupBy(p => p.GameId).ToDictionary(g => g.Key, g => g.OrderBy(p => p.Ply).ToList());
        var urls = games.ToDictionary(g => g.GameId, g => g.Url);
        return new VariationReview(scope, record, UsualLine(byGame), Trouble(byGame, urls));
    }

    /// <summary>The position part of a FEN (pieces, side to move, castling, en passant): the same position in any game.</summary>
    public static string PositionKey(string fen) => string.Join(' ', fen.Split(' ').Take(4));

    /// <summary>Whether <paramref name="uci"/> is a right answer for the spot.</summary>
    public static bool IsCorrect(TroubleSpot spot, string uci) => spot.Accept.Contains(uci, StringComparer.Ordinal);

    private static List<LineMove> UsualLine(Dictionary<string, List<VariationPly>> byGame)
    {
        var line = new List<LineMove>();
        var following = byGame.Values.ToList();
        for (var index = 0; ; index++)
        {
            var next = following.Where(g => g.Count > index).Select(g => g[index]).ToList();
            var top = next.GroupBy(p => p.San).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).FirstOrDefault();
            if (top is null || top.Count() < MinLineGames) break;
            var moves = top.ToList();
            var first = moves[0];
            line.Add(new LineMove(
                first.Ply, first.Fen, first.San, first.Uci, first.IsMine, moves.Count, next.Count,
                first.IsMine ? moves.Average(p => p.CpLoss) : 0,
                first.IsMine ? moves.Count(p => p.CpLoss >= SlipCp) : 0,
                MostCommon(moves.Select(p => p.BestSan)),
                MostCommon(moves.Select(p => p.BestUci))));
            following = following.Where(g => g.Count > index && g[index].San == top.Key).ToList();
        }

        return line;
    }

    private static List<TroubleSpot> Trouble(Dictionary<string, List<VariationPly>> byGame, Dictionary<string, string> urls)
    {
        var mine = byGame.Values.SelectMany(g => g).Where(p => p.IsMine).ToList();
        return mine
            .GroupBy(p => PositionKey(p.Fen))
            .Where(g => g.Any(p => p.CpLoss >= SlipCp) && (g.Count() >= 2 || g.First().Ply <= 2 * SingleSlipMaxMove))
            .Select(g =>
            {
                var visits = g.ToList();
                var slip = visits.Where(p => p.CpLoss >= SlipCp).OrderByDescending(p => p.CpLoss).First();
                var mistake = visits.Where(p => p.CpLoss >= SlipCp).GroupBy(p => p.San)
                    .OrderByDescending(m => m.Count()).ThenByDescending(m => m.Average(p => p.CpLoss)).ThenBy(m => m.Key, StringComparer.Ordinal).First();
                var bestUci = MostCommon(visits.Select(p => p.BestUci)) ?? "";
                var accept = new List<string>();
                if (bestUci.Length > 0) accept.Add(bestUci);
                accept.AddRange(visits.Where(p => p.CpLoss <= AcceptCp).Select(p => p.Uci).Distinct(StringComparer.Ordinal).Where(u => !accept.Contains(u)));
                return new TroubleSpot(
                    Key: g.Key,
                    Fen: slip.Fen,
                    Ply: slip.Ply,
                    Path: Path(byGame[slip.GameId], slip.Ply),
                    Reached: visits.Count,
                    Slips: visits.Count(p => p.CpLoss >= SlipCp),
                    MistakeSan: mistake.Key,
                    MistakeCount: mistake.Count(),
                    MistakeAvgCpLoss: mistake.Average(p => p.CpLoss),
                    BestSan: MostCommon(visits.Select(p => p.BestSan)) ?? "",
                    BestUci: bestUci,
                    Accept: accept,
                    ExampleUrl: urls.GetValueOrDefault(slip.GameId, ""));
            })
            .OrderByDescending(s => s.Slips)
            .ThenByDescending(s => s.Reached)
            .ThenByDescending(s => s.MistakeAvgCpLoss)
            .ThenBy(s => s.Ply)
            .Take(MaxTrouble)
            .ToList();
    }

    /// <summary>"1.e4 c6 2.Nc3 d5 3.Nf3": the moves of <paramref name="game"/> before <paramref name="ply"/>.</summary>
    private static string Path(List<VariationPly> game, int ply)
    {
        var text = new StringBuilder();
        foreach (var p in game.Where(p => p.Ply < ply))
        {
            if (p.Ply % 2 == 1) text.Append(CultureInfo.InvariantCulture, $"{(p.Ply + 1) / 2}.");
            text.Append(p.San).Append(' ');
        }

        return text.Length == 0 ? "Starting position" : text.ToString().TrimEnd();
    }

    private static string? MostCommon(IEnumerable<string?> values) =>
        values.Where(v => !string.IsNullOrEmpty(v)).GroupBy(v => v).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Key).FirstOrDefault();
}
