using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Abstractions;

public interface IDiagnosticsService
{
    /// <summary>Checks the data folder, database, analysis scripts and engines, and reports what is missing.</summary>
    Task<IReadOnlyList<DiagnosticCheck>> RunAsync(CancellationToken cancellationToken = default);
}
