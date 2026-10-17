namespace ChessCoach.Core.Training;

public static class DrillStreak
{
    /// <summary>
    /// Consecutive completed days ending today, or ending yesterday when today is not finished yet
    /// (an unfinished today does not break the streak).
    /// </summary>
    public static int Compute(IReadOnlySet<DateOnly> completedDays, DateOnly today)
    {
        var day = completedDays.Contains(today) ? today : today.AddDays(-1);
        var streak = 0;
        while (completedDays.Contains(day))
        {
            streak++;
            day = day.AddDays(-1);
        }

        return streak;
    }
}
