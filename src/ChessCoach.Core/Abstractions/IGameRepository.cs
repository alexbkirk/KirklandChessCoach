using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Abstractions;

public interface IGameRepository
{
    /// <summary>The headline numbers for the time class being viewed (all of them when "all games" is chosen).</summary>
    Task<DatabaseSummary> GetSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>The numbers for every game held, whatever is being viewed. Setup and diagnostics use this, so a view with no games never looks like a database with none.</summary>
    Task<DatabaseSummary> GetOverallSummaryAsync(CancellationToken cancellationToken = default);
}
