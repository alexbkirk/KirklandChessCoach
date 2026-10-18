namespace ChessCoach.Infrastructure;

/// <summary>Bound from the "ChessCoach" section of appsettings.json.</summary>
public sealed class ChessCoachOptions
{
    public const string SectionName = "ChessCoach";

    /// <summary>
    /// Folder holding the database, PGN archive, engines and logs. Blank means the per-user default
    /// (see <see cref="DefaultDataDirectory"/>).
    /// </summary>
    public string DataDirectory { get; set; } = "";

    /// <summary>Where the data lives when <see cref="DataDirectory"/> is blank: a per-user folder that needs no setup.</summary>
    public static string DefaultDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChessCoach");

    /// <summary>The configured data folder, or the default when none is configured.</summary>
    public string EffectiveDataDirectory => string.IsNullOrWhiteSpace(DataDirectory) ? DefaultDataDirectory : DataDirectory;

    public string DatabaseFileName { get; set; } = "xkirke_games.db";

    /// <summary>Folder with the Python analysis scripts. Resolved at startup when not set.</summary>
    public string? AnalysisDirectory { get; set; }

    /// <summary>Per-user folder for secrets such as the AI coach's API key; outside the data folder and the repository.</summary>
    public string SecretsDirectory { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChessCoach");

    public string DatabasePath => Path.Combine(EffectiveDataDirectory, DatabaseFileName);

    public string EngineDirectory => Path.Combine(EffectiveDataDirectory, "engine");

    public string StockfishPath => Path.Combine(EngineDirectory, "stockfish", "stockfish-windows-x86-64-universal.exe");

    public string Lc0Path => Path.Combine(EngineDirectory, "lc0", "lc0.exe");

    public string MaiaWeightsPath => Path.Combine(EngineDirectory, "maia", "maia-1100.pb.gz");

    /// <summary>The Python that runs the analysis scripts. Resolved at startup by <see cref="FindPython"/> when not set.</summary>
    public string? PythonPath { get; set; }

    /// <summary>
    /// The Python to run the analysis with, first existing of: the Python bundled in the install folder (python\python.exe),
    /// then the development virtual environment in the analysis folder (analysis\.venv).
    /// </summary>
    public static string? FindPython(string installDirectory, string? analysisDirectory)
    {
        var candidates = new List<string> { Path.Combine(installDirectory, "python", "python.exe") };
        if (analysisDirectory is not null)
        {
            candidates.Add(Path.Combine(analysisDirectory, ".venv", "Scripts", "python.exe"));
        }

        return candidates.FirstOrDefault(File.Exists);
    }
}
