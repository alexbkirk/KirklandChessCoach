using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Training;

/// <summary>Which candidate a slot uses: an index into the blunder list or into the safe-move list.</summary>
public sealed record PlannedSlot(DrillFormat Format, bool FromBlunders, int CandidateIndex);

/// <summary>
/// Chooses the day's drill deterministically from the date, so the same day always gives the same three slots.
/// Each slot gets a random format; Hang and Cct use blunders, Safe uses a blunder or a safe move with equal odds.
/// </summary>
public static class DrillPlanner
{
    public const int SlotsPerDay = 3;

    public static int SeedFor(DateOnly day) => day.DayNumber;

    public static IReadOnlyList<PlannedSlot> Plan(DateOnly day, int blunderCount, int safeCount)
    {
        if (blunderCount == 0)
        {
            return [];
        }

        // Seeded System.Random is stable across runs and .NET versions (it uses the legacy seeded algorithm).
#pragma warning disable CA5394 // not security sensitive
        var rng = new Random(SeedFor(day));
        var usedBlunders = new HashSet<int>();
        var usedSafe = new HashSet<int>();
        var slots = new List<PlannedSlot>();
        for (var i = 0; i < SlotsPerDay; i++)
        {
            var format = (DrillFormat)rng.Next(3);
            var fromBlunders = format != DrillFormat.Safe || safeCount == 0 || rng.Next(2) == 0;
            var count = fromBlunders ? blunderCount : safeCount;
            var used = fromBlunders ? usedBlunders : usedSafe;
            var index = rng.Next(count);
            for (var tries = 0; used.Contains(index) && tries < 60; tries++)
            {
                index = rng.Next(count);
            }
#pragma warning restore CA5394

            used.Add(index);
            slots.Add(new PlannedSlot(format, fromBlunders, index));
        }

        return slots;
    }
}
