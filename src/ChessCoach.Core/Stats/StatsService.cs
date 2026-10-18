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

    public async Task<Habits> GetHabitsAsync(string? color = null, CancellationToken cancellationToken = default)
    {
        var games = await repository.LoadGamesAsync(cancellationToken);
        return HabitsCalculator.Compute(games, color);
    }

    public async Task<IReadOnlyList<OpeningRow>> GetOpeningsAsync(string? color = null, int minGames = OpeningsTableCalculator.DefaultMinGames, CancellationToken cancellationToken = default)
    {
        var games = await repository.LoadGamesAsync(cancellationToken);
        return OpeningsTableCalculator.Compute(games, color, minGames);
    }
}
