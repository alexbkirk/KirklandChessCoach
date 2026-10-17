using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Abstractions;

public interface IGameRepository
{
    Task<DatabaseSummary> GetSummaryAsync(CancellationToken cancellationToken = default);
}
