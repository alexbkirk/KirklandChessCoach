namespace ChessCoach.Core.Setup;

/// <summary>How a component arrives: a zip archive (some of whose files we keep) or a single file.</summary>
public enum EngineDelivery
{
    Zip,
    File,
}

/// <summary>
/// One engine file the analysis needs. Each download is pinned by size and SHA-256, so the app only accepts the exact
/// build listed here. Under the data folder the file lands at <c>engine\FolderName\MainFile</c>, which is where the
/// analysis scripts look.
/// </summary>
/// <param name="ZipEntries">For zip deliveries: the file names to keep, or null to keep every file in the archive.</param>
public sealed record EngineComponent(
    string Name,
    string Description,
    string Url,
    string Sha256,
    long Size,
    EngineDelivery Delivery,
    string FolderName,
    string MainFile,
    IReadOnlyList<string>? ZipEntries = null)
{
    /// <summary>The main file's path relative to the data folder.</summary>
    public string RelativeMainPath => Path.Combine("engine", FolderName, MainFile);
}

public static class EngineCatalog
{
    public static IReadOnlyList<EngineComponent> All { get; } =
    [
        new(
            Name: "Stockfish 19",
            Description: "The chess engine that scores every move.",
            Url: "https://github.com/official-stockfish/Stockfish/releases/download/sf_19/stockfish-windows-x86-64-universal.zip",
            Sha256: "3c8bf1f9ea66a09350a40df4f632288285ac206d99f33ab5842c408fc30b48a7",
            Size: 81_431_614,
            Delivery: EngineDelivery.Zip,
            FolderName: "stockfish",
            MainFile: "stockfish-windows-x86-64-universal.exe",
            ZipEntries: ["stockfish-windows-x86-64-universal.exe"]),
        new(
            Name: "lc0 0.32.1",
            Description: "Runs the human-like Maia model.",
            Url: "https://github.com/LeelaChessZero/lc0/releases/download/v0.32.1/lc0-v0.32.1-windows-cpu-dnnl.zip",
            Sha256: "b9cfcfbd3dabffbfd452f6e8e087c22273721bef48eba19640b6d92006c142f5",
            Size: 24_001_097,
            Delivery: EngineDelivery.Zip,
            FolderName: "lc0",
            MainFile: "lc0.exe"),
        new(
            Name: "Maia 1100 weights",
            Description: "A model that plays like a 1100-rated player.",
            Url: "https://raw.githubusercontent.com/CSSLab/maia-chess/master/maia_weights/maia-1100.pb.gz",
            Sha256: "e1cf1cd0c96b8a4fa6a275f4b9fd54ed1ffebf9fe44641b9fceded310e9619c4",
            Size: 1_313_193,
            Delivery: EngineDelivery.File,
            FolderName: "maia",
            MainFile: "maia-1100.pb.gz"),
    ];

    /// <summary>The components whose main file is not yet present. <paramref name="exists"/> answers for a path relative to the data folder.</summary>
    public static IReadOnlyList<EngineComponent> Missing(Func<string, bool> exists) =>
        All.Where(c => !exists(c.RelativeMainPath)).ToList();
}
