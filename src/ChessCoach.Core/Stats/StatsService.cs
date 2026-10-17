using ChessCoach.Core.Abstractions;

namespace ChessCoach.Core.Stats;

/// <summary>Loads the games once per call and returns the stats each tab needs.</summary>
public sealed class StatsService(IStatsRepository repository)
{
    public async Task<Overview> GetOverviewAsync(string? color = null, CancellationToken cancellationToken = default)
    {
        var games = await repository.LoadGamesAsync(cancellationToken);
        return OverviewCalculator.Compute(games, color);
    }
}
