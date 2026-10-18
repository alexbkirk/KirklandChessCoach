using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Stats;

/// <summary>The opponents list (Study page) and the page for one opponent.</summary>
public sealed class OpponentService(IStatsRepository stats, IOpponentRepository opponents)
{
    public async Task<IReadOnlyList<OpponentRow>> ListAsync(string? color = null, int minGames = OpponentCalculator.DefaultMinGames, string? search = null, CancellationToken cancellationToken = default)
    {
        var games = await stats.LoadGamesAsync(cancellationToken);
        return OpponentCalculator.List(games, color, minGames, search);
    }

    public async Task<OpponentReport> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        var (games, plies) = await opponents.LoadAsync(name, cancellationToken);
        var baseline = await opponents.LoadBaselineAsync(cancellationToken);
        return OpponentCalculator.Compute(name, games, plies, baseline);
    }
}
