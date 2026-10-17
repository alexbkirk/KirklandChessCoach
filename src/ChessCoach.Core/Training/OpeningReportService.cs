using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Training;

public sealed class OpeningReportService(IOpeningRepository repository)
{
    public async Task<OpeningReport> GetAsync(CancellationToken cancellationToken = default)
    {
        var games = await repository.LoadAsync(cancellationToken);
        return OpeningReportCalculator.Compute(games);
    }
}
