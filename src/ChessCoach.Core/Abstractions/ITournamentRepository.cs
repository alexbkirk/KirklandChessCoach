using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Abstractions;

public interface ITournamentRepository
{
    /// <summary>Every game with a tournament set (newest first), whatever its time class.</summary>
    Task<IReadOnlyList<StatsGame>> LoadGamesAsync(CancellationToken cancellationToken = default);

    /// <summary>Analysed moves of the player's in tournament games, except each game's last.</summary>
    Task<IReadOnlyList<MoveSample>> LoadMoveSamplesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Game id to its tournament key (the games.tournament value), for every game with a tournament set.
    /// A manual over-the-board game's key is 'otb:' followed by its trimmed event name; a game from a
    /// chess.com tournament has its tournament URL as the key.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> LoadTournamentKeysAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores an over-the-board game with tournament key 'otb:' followed by its trimmed event name
    /// (so an empty event gives 'otb:'). Inserting the same game again is a no-op.
    /// </summary>
    Task AddManualGameAsync(ManualGame game, CancellationToken cancellationToken = default);
}
