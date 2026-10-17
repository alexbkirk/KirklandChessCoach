using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Training;

public sealed class WeeklyFocusService(IFocusRepository repository)
{
    public async Task<WeeklyFocus> GetAsync(CancellationToken cancellationToken = default)
    {
        var (games, blunders) = await repository.LoadAsync(cancellationToken);
        return WeeklyFocusCalculator.Compute(games, blunders);
    }
}
