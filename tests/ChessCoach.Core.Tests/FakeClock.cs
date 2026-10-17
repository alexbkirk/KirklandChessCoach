using ChessCoach.Core.Abstractions;

namespace ChessCoach.Core.Tests;

internal sealed class FakeClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;

    public DateOnly LocalToday => DateOnly.FromDateTime(UtcNow.LocalDateTime);
}
