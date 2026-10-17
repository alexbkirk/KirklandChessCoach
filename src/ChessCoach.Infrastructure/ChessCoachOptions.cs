namespace ChessCoach.Infrastructure;

/// <summary>Bound from the "ChessCoach" section of appsettings.json.</summary>
public sealed class ChessCoachOptions
{
    public const string SectionName = "ChessCoach";

    /// <summary>Folder holding the database, PGN archive, engines and logs.</summary>
    public string DataDirectory { get; set; } = "";

    public string DatabaseFileName { get; set; } = "xkirke_games.db";

    /// <summary>Folder with the Python analysis scripts. Resolved at startup when not set.</summary>
    public string? AnalysisDirectory { get; set; }

    /// <summary>Per-user folder for secrets such as the AI coach's API key; outside the data folder and the repository.</summary>
    public string SecretsDirectory { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChessCoach");

    public string DatabasePath => Path.Combine(DataDirectory, DatabaseFileName);

    public string EngineDirectory => Path.Combine(DataDirectory, "engine");

    public string StockfishPath => Path.Combine(EngineDirectory, "stockfish", "stockfish-windows-x86-64-universal.exe");

    public string Lc0Path => Path.Combine(EngineDirectory, "lc0", "lc0.exe");

    public string MaiaWeightsPath => Path.Combine(EngineDirectory, "maia", "maia-1100.pb.gz");

    public string? PythonPath => AnalysisDirectory is null ? null : Path.Combine(AnalysisDirectory, ".venv", "Scripts", "python.exe");
}
