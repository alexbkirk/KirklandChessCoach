using ChessCoach.Infrastructure;

namespace ChessCoach.Infrastructure.Tests;

public sealed class ChessCoachOptionsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "chesscoach-options-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void A_blank_data_folder_means_the_per_user_default()
    {
        var options = new ChessCoachOptions { DataDirectory = "  " };

        Assert.Equal(ChessCoachOptions.DefaultDataDirectory, options.EffectiveDataDirectory);
        Assert.Equal(Path.Combine(ChessCoachOptions.DefaultDataDirectory, "xkirke_games.db"), options.DatabasePath);
        Assert.EndsWith(Path.Combine("ChessCoach"), ChessCoachOptions.DefaultDataDirectory, StringComparison.Ordinal);
    }

    [Fact]
    public void A_configured_data_folder_is_used_as_given()
    {
        var options = new ChessCoachOptions { DataDirectory = @"D:\chess" };

        Assert.Equal(@"D:\chess", options.EffectiveDataDirectory);
        Assert.Equal(Path.Combine(@"D:\chess", "engine"), options.EngineDirectory);
    }

    [Fact]
    public void The_bundled_python_wins_over_the_development_virtual_environment()
    {
        var install = Path.Combine(_root, "install");
        var analysis = Path.Combine(_root, "analysis");
        Directory.CreateDirectory(Path.Combine(install, "python"));
        Directory.CreateDirectory(Path.Combine(analysis, ".venv", "Scripts"));
        File.WriteAllText(Path.Combine(install, "python", "python.exe"), "");
        File.WriteAllText(Path.Combine(analysis, ".venv", "Scripts", "python.exe"), "");

        Assert.Equal(Path.Combine(install, "python", "python.exe"), ChessCoachOptions.FindPython(install, analysis));
    }

    [Fact]
    public void The_development_virtual_environment_is_used_when_nothing_is_bundled()
    {
        var install = Path.Combine(_root, "install");
        var analysis = Path.Combine(_root, "analysis");
        Directory.CreateDirectory(install);
        Directory.CreateDirectory(Path.Combine(analysis, ".venv", "Scripts"));
        File.WriteAllText(Path.Combine(analysis, ".venv", "Scripts", "python.exe"), "");

        Assert.Equal(Path.Combine(analysis, ".venv", "Scripts", "python.exe"), ChessCoachOptions.FindPython(install, analysis));
        Assert.Null(ChessCoachOptions.FindPython(install, null));
    }
}
