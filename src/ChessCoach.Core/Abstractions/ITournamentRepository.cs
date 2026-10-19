using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Abstractions;

public interface ITournamentRepository
{
    /// <summary>Every game with a tournament set (newest first), whatever its time class.</summary>
    Task<IReadOnlyList<StatsGame>> LoadGamesAsync(CancellationToken cancellationToken = default);

    /// <summary>Analysed moves of the player's in tournament games, except each game's last.</summary>
    Task<IReadOnlyList<MoveSample>> LoadMoveSamplesAsync(CancellationToken cancellationToken = default);

    /// <summary>Stores an over-the-board game. Inserting the same game again is a no-op.</summary>
    Task AddManualGameAsync(ManualGame game, CancellationToken cancellationToken = default);
}
