using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Abstractions;

public interface IOpponentRepository
{
    /// <summary>Your games against <paramref name="name"/> (matched without regard to case) and all their moves.</summary>
    Task<(IReadOnlyList<OpponentGame> Games, IReadOnlyList<OpponentPly> Plies)> LoadAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Average loss and blunder share per phase over all your games, for you and for your opponents.</summary>
    Task<PhaseBaseline> LoadBaselineAsync(CancellationToken cancellationToken = default);
}
