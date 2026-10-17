using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Abstractions;

public interface IDrillRepository
{
    /// <summary>How many one-move blunders and how many safe moves are available as drill positions.</summary>
    Task<(int Blunders, int Safe)> CountCandidatesAsync(CancellationToken cancellationToken = default);

    /// <summary>The candidate at a stable index (ordered by game and ply) in the blunder or safe-move list.</summary>
    Task<DrillPosition?> GetCandidateAsync(bool blunder, int index, CancellationToken cancellationToken = default);

    /// <summary>The stored slots for a day, or null when the day has not been created yet.</summary>
    Task<IReadOnlyList<DrillSlot>?> LoadDayAsync(DateOnly day, CancellationToken cancellationToken = default);

    Task SaveNewDayAsync(DateOnly day, int seed, IReadOnlyList<DrillSlot> slots, CancellationToken cancellationToken = default);

    Task SaveSlotAsync(DateOnly day, DrillSlot slot, CancellationToken cancellationToken = default);

    /// <summary>Days on which all slots are done.</summary>
    Task<IReadOnlySet<DateOnly>> LoadCompletedDaysAsync(CancellationToken cancellationToken = default);
}
