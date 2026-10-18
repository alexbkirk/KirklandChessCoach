using ChessCoach.Core.Setup;

namespace ChessCoach.Core.Tests;

public sealed class EngineCatalogTests
{
    [Fact]
    public void Every_engine_is_pinned_by_a_full_sha256_and_a_size()
    {
        foreach (var engine in EngineCatalog.All)
        {
            Assert.Matches("^[0-9a-f]{64}$", engine.Sha256);
            Assert.True(engine.Size > 0, engine.Name);
            Assert.StartsWith("https://", engine.Url);
        }
    }

    [Fact]
    public void Each_engine_lands_where_the_analysis_scripts_look()
    {
        Assert.Equal(
            ["engine\\stockfish\\stockfish-windows-x86-64-universal.exe", "engine\\lc0\\lc0.exe", "engine\\maia\\maia-1100.pb.gz"],
            EngineCatalog.All.Select(e => e.RelativeMainPath));
    }

    [Fact]
    public void Missing_lists_only_the_engines_whose_main_file_is_absent()
    {
        var present = new HashSet<string> { EngineCatalog.All[0].RelativeMainPath };

        var missing = EngineCatalog.Missing(path => present.Contains(path));

        Assert.Equal(["lc0 0.32.1", "Maia 1100 weights"], missing.Select(e => e.Name));
    }

    [Fact]
    public void Nothing_is_missing_when_every_main_file_is_there()
    {
        Assert.Empty(EngineCatalog.Missing(_ => true));
    }
}
