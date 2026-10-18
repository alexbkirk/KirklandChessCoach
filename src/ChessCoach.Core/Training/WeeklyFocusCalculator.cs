using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Training;

/// <summary>
/// Finds the kind of blunder to work on. The window is the 30 days before your latest game, or your last 50 games when
/// those 30 days hold fewer than 15 games. Only blunders that cost at least <see cref="MinSwing"/> points of winning
/// chances count, so moves in positions that were already decided are left out. The focus is the most common kind with a
/// named tactic; counts are plain counts, with no comparison percentages (with this few blunders they are mostly chance).
/// </summary>
public static class WeeklyFocusCalculator
{
    public const int WindowDays = 30;
    public const int MinGamesInWindow = 15;
    public const int FallbackGameCount = 50;
    public const int ExampleCount = 5;
    public const int TopCount = 3;
    public const double MinSwing = 10;

    public static WeeklyFocus Compute(IReadOnlyList<FocusGame> games, IReadOnlyList<FocusBlunder> blunders)
    {
        var ordered = games.OrderBy(g => g.Date).ToList();
        if (ordered.Count == 0)
        {
            return new WeeklyFocus("no games yet", 0, null, null, 0, 0, 0, null, [], null, []);
        }

        var latest = ordered[^1].Date;
        var window = ordered.Where(g => g.Date >= latest.AddDays(-WindowDays)).ToList();
        var label = $"the last {WindowDays} days";
        if (window.Count < MinGamesInWindow)
        {
            window = ordered.TakeLast(FallbackGameCount).ToList();
            label = $"your last {window.Count} games";
        }

        var byId = ordered.ToDictionary(g => g.Id);
        var windowIds = window.Select(g => g.Id).ToHashSet();
        var inWindow = blunders.Where(b => windowIds.Contains(b.GameId)).ToList();
        var explained = inWindow.Where(b => b.Swing is not null).ToList();
        var counted = explained.Where(b => b.Swing >= MinSwing).ToList();

        var kinds = counted
            .GroupBy(b => LeakCatalog.For(b.Side, b.Motif))
            .Select(g => new LeakCount(g.Key.Key, g.Key.Label, g.Count()))
            .ToList();
        var named = kinds.Where(k => k.Key != LeakCatalog.Other.Key)
            .OrderByDescending(k => k.Count).ThenBy(k => k.Label, StringComparer.Ordinal)
            .ToList();
        var focus = named.Count > 0 ? LeakCatalog.Find(named[0].Key) : null;

        var examples = focus is null
            ? []
            : counted
                .Where(b => LeakCatalog.For(b.Side, b.Motif).Key == focus.Key)
                .OrderByDescending(b => byId[b.GameId].Date)
                .ThenByDescending(b => b.Ply)
                .Take(ExampleCount)
                .Select(b =>
                {
                    var game = byId[b.GameId];
                    return new FocusExample(game.Date, game.Opponent, (b.Ply + 1) / 2, game.Url, b.San, b.Headline, b.Fen, b.Color, b.BestUci, b.BestSan);
                })
                .ToList();

        return new WeeklyFocus(
            Window: label,
            GameCount: window.Count,
            From: window[0].Date,
            To: window[^1].Date,
            BlunderCount: counted.Count,
            OtherCount: kinds.FirstOrDefault(k => k.Key == LeakCatalog.Other.Key)?.Count ?? 0,
            UnexplainedCount: inWindow.Count - explained.Count,
            Focus: focus,
            Top: named.Take(TopCount).ToList(),
            OverallTop: OverallTop(blunders),
            Examples: examples);
    }

    /// <summary>The most common named kind across every explained, costly blunder you have made.</summary>
    private static LeakInfo? OverallTop(IReadOnlyList<FocusBlunder> blunders) =>
        blunders
            .Where(b => b.Swing >= MinSwing)
            .Select(b => LeakCatalog.For(b.Side, b.Motif))
            .Where(k => k.Key != LeakCatalog.Other.Key)
            .GroupBy(k => k)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key.Label, StringComparer.Ordinal)
            .Select(g => g.Key)
            .FirstOrDefault();
}
