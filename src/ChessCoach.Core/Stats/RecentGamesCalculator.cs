using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Stats;

/// <param name="Games">The newest games first, up to the count asked for.</param>
/// <param name="Total">All games that match, so the caller can tell whether more are left.</param>
public sealed record RecentGames(IReadOnlyList<StatsGame> Games, int Total)
{
    public int Remaining => Total - Games.Count;
}

public static class RecentGamesCalculator
{
    public const int PageSize = 50;

    /// <summary>The newest <paramref name="count"/> games, or the newest as <paramref name="color"/> ("white" or "black") when given.</summary>
    public static RecentGames Compute(IReadOnlyList<StatsGame> games, string? color = null, int count = PageSize)
    {
        var chosen = games
            .Where(g => color is null || g.Color == color)
            .OrderByDescending(g => g.Date).ThenByDescending(g => g.Time)
            .ToList();

        return new RecentGames(chosen.Take(Math.Max(0, count)).ToList(), chosen.Count);
    }
}
