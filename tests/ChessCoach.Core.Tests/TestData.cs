using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Tests;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    public static Puzzle Puzzle(string id, int cpLoss = 300, params string[] accept) =>
        new(id, "g", 1, "8/8/8/8/8/8/8/8 w - - 0 1", "white", "Nf3", accept.FirstOrDefault() ?? "e2e4",
            accept.Length == 0 ? ["e2e4"] : accept, cpLoss, "2026-01-01", "opp", "https://x",
            new CoachNote("h", [], "e4", "e4", "", "", "", "+1.0", "-1.0"));
}
