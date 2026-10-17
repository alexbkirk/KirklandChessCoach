namespace ChessCoach.Core.Abstractions;

/// <summary>Time source, so logic that depends on "now" or "today" can be tested.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }

    /// <summary>Today's date in the user's local time zone (the daily drill changes at local midnight).</summary>
    DateOnly LocalToday { get; }
}
