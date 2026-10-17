using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Stats;

/// <summary>One opening family and your record in it.</summary>
public sealed record OpeningRow(string Family, Record Record);

/// <summary>The Openings tab: every opening family you have played, filtered by colour and a minimum number of games.</summary>
public static class OpeningsTableCalculator
{
    public const int DefaultMinGames = 10;

    /// <summary>Rows ordered by games played (most first), then by name. Families with fewer than <paramref name="minGames"/> games are left out.</summary>
    public static IReadOnlyList<OpeningRow> Compute(IReadOnlyList<StatsGame> games, string? color = null, int minGames = DefaultMinGames) =>
        games
            .Where(g => color is null || g.Color == color)
            .GroupBy(g => g.Family)
            .Select(group => new OpeningRow(group.Key, OverviewCalculator.RecordOf(group.ToList())))
            .Where(row => row.Record.Games >= Math.Max(1, minGames))
            .OrderByDescending(row => row.Record.Games)
            .ThenBy(row => row.Family, StringComparer.Ordinal)
            .ToList();
}
