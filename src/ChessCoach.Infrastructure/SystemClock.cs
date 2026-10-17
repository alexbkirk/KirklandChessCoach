using ChessCoach.Core.Abstractions;

namespace ChessCoach.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateOnly LocalToday => DateOnly.FromDateTime(DateTime.Now);
}
