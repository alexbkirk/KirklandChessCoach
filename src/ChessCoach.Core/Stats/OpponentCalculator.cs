using ChessCoach.Core.Domain;
using ChessCoach.Core.Training;

namespace ChessCoach.Core.Stats;

/// <summary>
/// The opponent page and the opponents list. Phases are by move number: opening up to move 12, middlegame up to
/// move 30, then late game (the same split the coach notes use for time spent).
/// </summary>
public static class OpponentCalculator
{
    public const string Opening = "Opening";
    public const string Middlegame = "Middlegame";
    public const string Late = "Late game";
    public const int PunishedCp = 100;
    public const int MaxMoments = 10;
    public const int OpeningPlies = 30;
    public const int DefaultMinGames = 3;

    public static readonly IReadOnlyList<string> Phases = [Opening, Middlegame, Late];

    public static string PhaseOf(int ply)
    {
        var move = (ply + 1) / 2;
        return move <= 12 ? Opening : move <= 30 ? Middlegame : Late;
    }

    /// <summary>Opponents you have played at least <paramref name="minGames"/> times, most games first. Names match without regard to case.</summary>
    public static IReadOnlyList<OpponentRow> List(IReadOnlyList<StatsGame> games, string? color = null, int minGames = DefaultMinGames, string? search = null) =>
        games
            .Where(g => color is null || g.Color == color)
            .Where(g => string.IsNullOrWhiteSpace(search) || g.Opponent.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))
            .GroupBy(g => g.Opponent, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var list = group.OrderBy(g => g.Date).ThenBy(g => g.Time, StringComparer.Ordinal).ToList();
                return new OpponentRow(list[^1].Opponent, OverviewCalculator.RecordOf(list), list[^1].Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), list.Average(g => g.OppRating));
            })
            .Where(r => r.Record.Games >= Math.Max(1, minGames))
            .OrderByDescending(r => r.Record.Games)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static OpponentReport Compute(string name, IReadOnlyList<OpponentGame> games, IReadOnlyList<OpponentPly> plies, PhaseBaseline baseline)
    {
        var ordered = games.OrderByDescending(g => g.Date, StringComparer.Ordinal).ThenByDescending(g => g.Time, StringComparer.Ordinal).ToList();
        var byId = games.ToDictionary(g => g.GameId);
        var byGame = plies.GroupBy(p => p.GameId).ToDictionary(g => g.Key, g => g.OrderBy(p => p.Ply).ToList());
        var counted = plies.Where(p => !p.IsLast).ToList();

        var rows = ordered.Select(g =>
        {
            var moves = byGame.GetValueOrDefault(g.GameId, []);
            return new OpponentGameRow(g, moves.Count(p => p.IsMine && !p.IsLast && p.Class == "blunder"), moves.Count(p => !p.IsMine && !p.IsLast && p.Class == "blunder"));
        }).ToList();

        var phases = Phases.Select(phase => new PhaseComparison(
            phase,
            Rate(counted.Where(p => p.IsMine && PhaseOf(p.Ply) == phase)),
            Rate(counted.Where(p => !p.IsMine && PhaseOf(p.Ply) == phase)),
            baseline.You.GetValueOrDefault(phase, PhaseRate.Empty),
            baseline.Opponents.GetValueOrDefault(phase, PhaseRate.Empty))).ToList();

        var yourBlunders = counted
            .Where(p => p.IsMine && p.Class == "blunder")
            .OrderByDescending(Swing).ThenByDescending(p => p.CpLoss).ThenByDescending(p => byId[p.GameId].Date, StringComparer.Ordinal)
            .Take(MaxMoments)
            .Select(p => new OpponentMoment(byId[p.GameId], p.Ply, p.San, p.Fen, p.BestSan, p.CpLoss, p.Clock, p.NoteHeadline, p.Chances?.Before, p.Chances?.After))
            .ToList();

        var theirBlunders = counted
            .Where(p => !p.IsMine && p.Class == "blunder")
            .Select(p =>
            {
                var reply = byGame[p.GameId].FirstOrDefault(r => r.Ply == p.Ply + 1);
                return new OpponentMoment(byId[p.GameId], p.Ply, p.San, p.Fen, p.BestSan, p.CpLoss, p.Clock, null, p.Chances?.Before, p.Chances?.After,
                    reply?.San, reply?.BestSan, Punished: reply is not null && (reply.CpLoss < PunishedCp || reply.San == reply.BestSan));
            })
            .OrderBy(m => m.Punished)
            .ThenByDescending(m => m.Before - m.After ?? 0)
            .ThenByDescending(m => m.CpLoss)
            .ThenByDescending(m => m.Game.Date, StringComparer.Ordinal)
            .ToList();

        var openingPlies = plies.Where(p => p.Ply <= OpeningPlies)
            .Select(p => new VariationPly(p.GameId, p.Ply, p.Fen, p.San, p.Uci, p.IsMine, p.BestSan, p.BestUci, p.CpLoss))
            .ToList();
        var urls = games.ToDictionary(g => g.GameId, g => g.Url);

        var openings = games
            .GroupBy(g => (g.Family, g.Color))
            .Select(g => new OpponentOpening(g.Key.Family, g.Key.Color, RecordOf(g)))
            .OrderByDescending(o => o.Record.Games).ThenBy(o => o.Family, StringComparer.Ordinal)
            .ToList();

        var endings = games
            .GroupBy(g => g.Ending)
            .Select(g => new RecordSplit(g.Key, RecordOf(g)))
            .OrderByDescending(e => e.Record.Games).ThenBy(e => e.Label, StringComparer.Ordinal)
            .ToList();

        return new OpponentReport(
            Name: ordered.Count > 0 ? ordered[0].Opponent : name,
            Record: RecordOf(games),
            FirstDate: ordered.Count > 0 ? ordered[^1].Date : "",
            LastDate: ordered.Count > 0 ? ordered[0].Date : "",
            AvgTheirRating: games.Count > 0 ? games.Average(g => g.OppRating) : 0,
            AvgRatingGap: games.Count > 0 ? games.Average(g => g.OppRating - g.MyRating) : 0,
            Games: rows,
            Phases: phases,
            YourBlunders: yourBlunders,
            TheirBlunders: theirBlunders,
            YourOpeningSlips: OpeningVariationCalculator.TroubleSpots(openingPlies, urls, mine: true),
            TheirOpeningSlips: OpeningVariationCalculator.TroubleSpots(openingPlies, urls, mine: false),
            Openings: openings,
            Endings: endings);
    }

    /// <summary>Winning chances the mover lost with the move; 0 when the evaluations are unknown.</summary>
    private static double Swing(OpponentPly p) => p.Chances is { } c ? c.Before - c.After : 0;

    /// <summary>Average loss and blunder share of the given moves (final moves of games already left out).</summary>
    public static PhaseRate Rate(IEnumerable<OpponentPly> moves)
    {
        var list = moves.ToList();
        return list.Count == 0
            ? PhaseRate.Empty
            : new PhaseRate(list.Count, list.Average(p => p.CpLoss), 100.0 * list.Count(p => p.Class == "blunder") / list.Count);
    }

    private static Record RecordOf(IEnumerable<OpponentGame> games)
    {
        var list = games.ToList();
        return new Record(list.Count(g => g.Result == "win"), list.Count(g => g.Result == "draw"), list.Count(g => g.Result == "loss"));
    }
}
