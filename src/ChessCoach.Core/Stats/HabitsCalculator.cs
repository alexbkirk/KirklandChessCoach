using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Stats;

/// <summary>One row of a habits view: a label and the record behind it.</summary>
public sealed record HabitRow(string Label, Record Record);

public sealed record Habits(
    IReadOnlyList<HabitRow> Endings,
    IReadOnlyList<HabitRow> Hours,
    IReadOnlyList<HabitRow> RatingGap,
    IReadOnlyList<HabitRow> GameOfDay);

public static class HabitsCalculator
{
    /// <summary>Hours with fewer games than this are shown faded in the UI rather than hidden.</summary>
    public const int ReliableGames = 5;

    private static readonly (int Min, int Max, string Label)[] Gaps =
    [
        (int.MinValue, -100, "100+ lower"),
        (-100, -30, "30–100 lower"),
        (-30, 30, "within 30"),
        (30, 100, "30–100 higher"),
        (100, int.MaxValue, "100+ higher"),
    ];

    /// <summary>Habit stats for the chosen games, or all games when <paramref name="color"/> is null.</summary>
    public static Habits Compute(IReadOnlyList<StatsGame> games, string? color = null)
    {
        var chosen = games
            .Where(g => color is null || g.Color == color)
            .OrderBy(g => g.Date).ThenBy(g => g.Time)
            .ToList();

        var endings = chosen
            .GroupBy(g => g.Ending)
            .OrderByDescending(g => g.Count())
            .Select(g => new HabitRow(g.Key, OverviewCalculator.RecordOf(g.ToList())))
            .ToList();

        // three-hour buckets of the UTC start time, empty buckets included so the gaps show
        var hours = Enumerable.Range(0, 8)
            .Select(i => new HabitRow(
                $"{i * 3:00}:00",
                OverviewCalculator.RecordOf(chosen.Where(g => HourBucket(g.Time) == i).ToList())))
            .ToList();

        var ratingGap = Gaps
            .Select(b => new HabitRow(b.Label, OverviewCalculator.RecordOf(
                chosen.Where(g => InGap(g.OppRating - g.MyRating, b.Min, b.Max)).ToList())))
            .ToList();

        // the nth game played on the same day: 1, 2, 3, 4, 5, then 6 or more
        var gameOfDay = new List<HabitRow>();
        foreach (var (label, n) in new[] { ("1st", 1), ("2nd", 2), ("3rd", 3), ("4th", 4), ("5th", 5), ("6th or later", 6) })
        {
            var inSlot = PositionsOfDay(chosen).Where(p => n == 6 ? p.Position >= 6 : p.Position == n).Select(p => p.Game).ToList();
            gameOfDay.Add(new HabitRow(label, OverviewCalculator.RecordOf(inSlot)));
        }

        return new Habits(endings, hours, ratingGap, gameOfDay);
    }

    private static int HourBucket(string time) =>
        int.TryParse(time.AsSpan(0, Math.Min(2, time.Length)), out var hour) ? Math.Clamp(hour / 3, 0, 7) : 0;

    private static bool InGap(int difference, int min, int max) => difference >= min && difference < max;

    /// <summary>Pairs each game (in date and time order) with its position among the games played that day.</summary>
    private static IEnumerable<(StatsGame Game, int Position)> PositionsOfDay(IReadOnlyList<StatsGame> ordered)
    {
        var position = 0;
        DateOnly? day = null;
        foreach (var game in ordered)
        {
            position = game.Date == day ? position + 1 : 1;
            day = game.Date;
            yield return (game, position);
        }
    }
}
