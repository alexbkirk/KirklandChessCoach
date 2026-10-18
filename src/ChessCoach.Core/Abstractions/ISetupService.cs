using ChessCoach.Core.Setup;

namespace ChessCoach.Core.Abstractions;

/// <summary>Reads and changes what the first-run setup still needs.</summary>
public interface ISetupService
{
    Task<SetupStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>Saves the chess.com username. Returns null when saved, or the reason it was refused.</summary>
    Task<string?> SaveUsernameAsync(string? username, CancellationToken cancellationToken = default);
}

/// <summary>Downloads one engine and checks it (see <see cref="EngineCatalog"/>).</summary>
public interface IEngineInstaller
{
    Task InstallAsync(EngineComponent component, IProgress<EngineProgress>? progress = null, CancellationToken cancellationToken = default);
}

/// <summary>Fetches and analyses one player's games, with progress.</summary>
public interface IImportRunner
{
    Task RunAsync(string username, IProgress<ImportProgress>? progress = null, CancellationToken cancellationToken = default);
}

/// <summary>One progress update from the import: the stage (fetch, analyze, tag, explain, coach, done) and how far it is.</summary>
public sealed record ImportProgress(string Stage, int Done, int Total, string Message);
