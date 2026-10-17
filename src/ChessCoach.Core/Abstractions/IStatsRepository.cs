using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Abstractions;

public interface IStatsRepository
{
    /// <summary>Every game (oldest first) with the fields the stats tabs use.</summary>
    Task<IReadOnlyList<StatsGame>> LoadGamesAsync(CancellationToken cancellationToken = default);
}
