using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Stats;

/// <summary>One of the most recent games, for the tournament prep list.</summary>
public sealed record TournamentPrepGame(DateOnly Date, string Opponent, string Color, string Result, string Url);

/// <summary>Summary of the games a player is preparing for a tournament, from every game they have.</summary>
/// <param name="Games">Number of games counted.</param>
/// <param name="ByColor">Record as White and as Black.</param>
/// <param name="Openings">Every opening family, most played first.</param>
/// <param name="Recent">Up to five newest games, newest first.</param>
/// <param name="OverTheBoard">Games with no link (empty Url).</param>
/// <param name="BlunderRate">Blunders divided by the moves of these games; 0 when there are no moves.</param>
public sealed record TournamentPrep(
    int Games,
    Record Record,
    IReadOnlyList<RecordSplit> ByColor,
    IReadOnlyList<OpeningRow> Openings,
    IReadOnlyList<TournamentPrepGame> Recent,
    int OverTheBoard,
    double BlunderRate);

public static class TournamentPrepCalculator
{
    private const int RecentCount = 5;
    private static readonly string[] Colors = ["white", "black"];

    /// <summary>Summarises all of <paramref name="games"/>. Moves count only when their game is in <paramref name="games"/>.</summary>
    public static TournamentPrep Compute(IReadOnlyList<StatsGame> games, IReadOnlyList<MoveSample> moves)
    {
        var ids = games.Select(g => g.Id).ToHashSet();
        var gameMoves = moves.Where(m => ids.Contains(m.GameId)).ToList();
        var blunders = gameMoves.Count(m => m.IsBlunder);

        return new TournamentPrep(
            Games: games.Count,
            Record: OverviewCalculator.RecordOf(games),
            ByColor: Colors
                .Select(c => new RecordSplit(c == "white" ? "White" : "Black", OverviewCalculator.RecordOf(games.Where(g => g.Color == c).ToList())))
                .ToList(),
            Openings: games
                .GroupBy(g => g.Family)
                .Select(group => new OpeningRow(group.Key, OverviewCalculator.RecordOf(group.ToList())))
                .OrderByDescending(row => row.Record.Games)
                .ThenBy(row => row.Family, StringComparer.Ordinal)
                .ToList(),
            Recent: games
                .OrderByDescending(g => g.Date)
                .ThenByDescending(g => g.Time, StringComparer.Ordinal)
                .Take(RecentCount)
                .Select(g => new TournamentPrepGame(g.Date, g.Opponent, g.Color, g.Result, g.Url))
                .ToList(),
            OverTheBoard: games.Count(g => g.Url == ""),
            BlunderRate: gameMoves.Count == 0 ? 0 : (double)blunders / gameMoves.Count);
    }
}
