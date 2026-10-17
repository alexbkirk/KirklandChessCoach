using System.Globalization;
using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Domain;
using ChessCoach.Infrastructure.Data;
using Dapper;
using Microsoft.Extensions.Options;

namespace ChessCoach.Infrastructure.Diagnostics;

public sealed class DiagnosticsService(
    IOptions<ChessCoachOptions> options,
    SqliteConnectionFactory connections,
    IGameRepository games) : IDiagnosticsService
{
    public async Task<IReadOnlyList<DiagnosticCheck>> RunAsync(CancellationToken cancellationToken = default)
    {
        var o = options.Value;
        var checks = new List<DiagnosticCheck>
        {
            Folder("Data folder", o.DataDirectory),
        };

        if (File.Exists(o.DatabasePath))
        {
            var megabytes = new FileInfo(o.DatabasePath).Length / 1024d / 1024d;
            checks.Add(new("Database file", DiagnosticStatus.Ok, string.Create(CultureInfo.InvariantCulture, $"{o.DatabasePath} ({megabytes:F1} MB)")));
            try
            {
                var summary = await games.GetSummaryAsync(cancellationToken);
                checks.Add(new("Games", DiagnosticStatus.Ok,
                    string.Create(CultureInfo.InvariantCulture, $"{summary.GameCount:N0} games, {summary.MoveCount:N0} analysed moves, {summary.FirstGameDate} to {summary.LastGameDate}")));

                await using var connection = await connections.OpenAsync(cancellationToken);
                var mode = await connection.ExecuteScalarAsync<string>("PRAGMA journal_mode;");
                checks.Add(new("Journal mode", string.Equals(mode, "wal", StringComparison.OrdinalIgnoreCase) ? DiagnosticStatus.Ok : DiagnosticStatus.Warning, mode ?? "unknown"));
                var version = await connection.ExecuteScalarAsync<int?>("SELECT MAX(version) FROM schema_version");
                checks.Add(new("Schema version", DiagnosticStatus.Ok, version?.ToString(CultureInfo.InvariantCulture) ?? "none"));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                checks.Add(new("Database", DiagnosticStatus.Error, ex.Message));
            }
        }
        else
        {
            checks.Add(new("Database file", DiagnosticStatus.Error, $"Not found: {o.DatabasePath}"));
        }

        checks.Add(o.AnalysisDirectory is null
            ? new DiagnosticCheck("Analysis scripts", DiagnosticStatus.Warning, "Folder not found (expected an 'analysis' folder with refresh.py next to the app or repo)")
            : File.Exists(Path.Combine(o.AnalysisDirectory, "refresh.py"))
                ? new DiagnosticCheck("Analysis scripts", DiagnosticStatus.Ok, o.AnalysisDirectory)
                : new DiagnosticCheck("Analysis scripts", DiagnosticStatus.Error, $"refresh.py missing in {o.AnalysisDirectory}"));
        checks.Add(o.PythonPath is { } python && File.Exists(python)
            ? new DiagnosticCheck("Python environment", DiagnosticStatus.Ok, python)
            : new DiagnosticCheck("Python environment", DiagnosticStatus.Warning, "analysis\\.venv not found: run scripts\\setup.ps1"));
        checks.Add(FileCheck("Stockfish", o.StockfishPath));
        checks.Add(FileCheck("lc0", o.Lc0Path));
        checks.Add(FileCheck("Maia 1100 weights", o.MaiaWeightsPath));
        return checks;
    }

    private static DiagnosticCheck Folder(string name, string path) =>
        Directory.Exists(path)
            ? new DiagnosticCheck(name, DiagnosticStatus.Ok, path)
            : new DiagnosticCheck(name, DiagnosticStatus.Error, $"Not found: {path}");

    private static DiagnosticCheck FileCheck(string name, string path) =>
        File.Exists(path)
            ? new DiagnosticCheck(name, DiagnosticStatus.Ok, path)
            : new DiagnosticCheck(name, DiagnosticStatus.Error, $"Not found: {path}");
}
