using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Training;

/// <summary>
/// Finds the leak to work on. The window is the last 30 days before your latest game, or your last 50 games
/// when that window has fewer than 15 games. The focus is the most frequent tag in the window, excluding positional slips.
/// </summary>
public static class WeeklyFocusCalculator
{
    public const int WindowDays = 30;
    public const int MinGamesInWindow = 15;
    public const int FallbackGameCount = 50;
    public const int ExampleCount = 5;

    public static WeeklyFocus Compute(IReadOnlyList<FocusGame> games, IReadOnlyList<FocusBlunder> blunders)
    {
        var ordered = games.OrderBy(g => g.Date).ToList();
        if (ordered.Count == 0)
        {
            return new WeeklyFocus("no games yet", 0, null, null, null, null, [], []);
        }

        var latest = ordered[^1].Date;
        var window = ordered.Where(g => g.Date >= latest.AddDays(-WindowDays)).ToList();
        var label = $"the last {WindowDays} days";
        if (window.Count < MinGamesInWindow)
        {
            window = ordered.TakeLast(FallbackGameCount).ToList();
            label = $"your last {FallbackGameCount} games";
        }

        var windowIds = window.Select(g => g.Id).ToHashSet();
        var inWindow = blunders.Where(b => windowIds.Contains(b.GameId)).ToList();
        var windowCounts = CountTags(inWindow);
        var lifetimeCounts = CountTags(blunders);

        var stats = TagCatalog.All.Select(tag =>
        {
            var count = windowCounts.GetValueOrDefault(tag.Key);
            var perGame = (double)count / window.Count;
            var lifetimeRate = lifetimeCounts.GetValueOrDefault(tag.Key) / (double)ordered.Count;
            double? delta = lifetimeRate > 0 ? (perGame / lifetimeRate - 1) * 100 : null;
            return new TagStat(tag.Key, tag.Label, count, perGame, delta);
        }).ToList();

        var top = stats.Where(s => s.Key != TagCatalog.Other).OrderByDescending(s => s.Count).FirstOrDefault();
        if (top is null || top.Count == 0)
        {
            return new WeeklyFocus(label, window.Count, null, null, null, null, stats, []);
        }

        var info = TagCatalog.Find(top.Key)!;
        var byId = ordered.ToDictionary(g => g.Id);
        var examples = inWindow
            .Where(b => b.Tags.Contains(top.Key))
            .OrderByDescending(b => byId[b.GameId].Date)
            .ThenByDescending(b => b.Ply)
            .Take(ExampleCount)
            .Select(b => new FocusExample(byId[b.GameId].Date, byId[b.GameId].Opponent, (b.Ply + 1) / 2, byId[b.GameId].Url))
            .ToList();

        return new WeeklyFocus(label, window.Count, top.Key, info.Title, info.Description, info.Advice, stats, examples);
    }

    private static Dictionary<string, int> CountTags(IEnumerable<FocusBlunder> blunders)
    {
        var counts = new Dictionary<string, int>();
        foreach (var blunder in blunders)
        {
            foreach (var tag in blunder.Tags)
            {
                counts[tag] = counts.GetValueOrDefault(tag) + 1;
            }
        }

        return counts;
    }
}
