using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Abstractions;

public interface IStatsRepository
{
    /// <summary>Every game (oldest first) with the fields the stats tabs use.</summary>
    Task<IReadOnlyList<StatsGame>> LoadGamesAsync(CancellationToken cancellationToken = default);

    /// <summary>Every analysed move of the player's, except each game's last.</summary>
    Task<IReadOnlyList<MoveSample>> LoadMoveSamplesAsync(CancellationToken cancellationToken = default);
}
