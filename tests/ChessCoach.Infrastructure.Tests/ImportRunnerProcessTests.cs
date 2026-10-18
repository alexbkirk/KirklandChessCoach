using ChessCoach.Core.Abstractions;
using ChessCoach.Infrastructure;
using ChessCoach.Infrastructure.Setup;
using Microsoft.Extensions.Options;

namespace ChessCoach.Infrastructure.Tests;

/// <summary>
/// Runs ImportRunner against a stand-in refresh.py, with the development Python (analysis\.venv, made by scripts\setup.ps1).
/// The stand-in script prints what the real one prints, so the launch, progress, failure and pause paths are all real.
/// </summary>
public sealed class ImportRunnerProcessTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "chesscoach-import-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static string DevelopmentPython()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var python = ChessCoachOptions.FindPython(dir.FullName, Path.Combine(dir.FullName, "analysis"));
            if (python is not null && File.Exists(Path.Combine(dir.FullName, "analysis", "refresh.py"))) return python;
        }

        throw new InvalidOperationException("analysis\\.venv was not found: run scripts\\setup.ps1 before the tests.");
    }

    private ImportRunner Runner(string script, ChessCoach.Core.Settings.TimeClassSettings? timeClasses = null)
    {
        var analysis = Path.Combine(_root, "analysis");
        Directory.CreateDirectory(analysis);
        File.WriteAllText(Path.Combine(analysis, "refresh.py"), script);
        return new ImportRunner(Options.Create(new ChessCoachOptions
        {
            DataDirectory = Path.Combine(_root, "data"),
            AnalysisDirectory = analysis,
            PythonPath = DevelopmentPython(),
        }), timeClasses);
    }

    [Fact]
    public async Task Progress_lines_are_reported_and_other_output_is_ignored()
    {
        var runner = Runner("""
            import json, sys
            assert sys.argv[sys.argv.index("--user") + 1] == "xkirke"
            assert sys.argv[sys.argv.index("--progress") + 1] == "jsonl"
            print("plain text the app should ignore", flush=True)
            print(json.dumps({"stage": "fetch", "done": 1, "total": 2, "message": "Downloaded month 1 of 2"}), flush=True)
            print(json.dumps({"stage": "done", "done": 1, "total": 1, "message": "Done."}), flush=True)
            """);
        var updates = new List<ImportProgress>();

        await runner.RunAsync("xkirke", new SynchronousProgress(updates));

        Assert.Equal(["fetch", "done"], updates.Select(u => u.Stage));
        Assert.Equal(new ImportProgress("fetch", 1, 2, "Downloaded month 1 of 2"), updates[0]);
    }

    [Fact]
    public async Task A_failing_script_reports_its_error_output()
    {
        var runner = Runner("""
            import sys
            sys.stderr.write("chess.com could not be reached\n")
            sys.exit(3)
            """);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync("xkirke"));

        Assert.Contains("exit code 3", error.Message);
        Assert.Contains("chess.com could not be reached", error.Message);
    }

    [Fact]
    public async Task Pausing_stops_the_script_and_reports_a_cancellation()
    {
        var marker = Path.Combine(_root, "still-running.txt");
        var runner = Runner($$"""
            import json, time
            print(json.dumps({"stage": "analyze", "done": 0, "total": 9, "message": "Analysing"}), flush=True)
            time.sleep(30)
            open(r"{{marker}}", "w").write("not stopped")
            """);
        using var pause = new CancellationTokenSource();
        var started = new TaskCompletionSource();

        var run = runner.RunAsync("xkirke", new SynchronousProgress([], () => started.TrySetResult()), pause.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(20));
        pause.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.False(File.Exists(marker));
    }

    [Fact]
    public async Task Without_python_or_scripts_the_reason_is_plain()
    {
        var noPython = new ImportRunner(Options.Create(new ChessCoachOptions { AnalysisDirectory = _root }));
        var noScripts = new ImportRunner(Options.Create(new ChessCoachOptions { PythonPath = "python.exe" }));

        Assert.Contains("Python", (await Assert.ThrowsAsync<InvalidOperationException>(() => noPython.RunAsync("xkirke"))).Message);
        Assert.Contains("analysis scripts", (await Assert.ThrowsAsync<InvalidOperationException>(() => noScripts.RunAsync("xkirke"))).Message);
    }

    /// <summary>Records updates as they arrive (Progress&lt;T&gt; would post them later, after the test checks).</summary>
    private sealed class SynchronousProgress(List<ImportProgress> updates, Action? onFirst = null) : IProgress<ImportProgress>
    {
        public void Report(ImportProgress value)
        {
            updates.Add(value);
            onFirst?.Invoke();
        }
    }

    private sealed class MemoryStore : ChessCoach.Core.Abstractions.IAppSettingsStore
    {
        public Dictionary<string, string> Values { get; } = [];

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(Values.TryGetValue(key, out var v) ? v : null);

        public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            Values[key] = value;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task The_time_classes_and_the_backfill_are_passed_to_the_script_and_the_backfill_is_cleared_after_success()
    {
        var settings = new ChessCoach.Core.Settings.TimeClassSettings(new MemoryStore());
        await settings.SetImportClassesAsync(["blitz"]);
        var runner = Runner("""
            import json, sys
            assert sys.argv[sys.argv.index("--time-classes") + 1] == "rapid,blitz", sys.argv
            assert "--backfill" in sys.argv, sys.argv
            print(json.dumps({"stage": "done", "done": 1, "total": 1, "message": "Done."}), flush=True)
            """, settings);

        await runner.RunAsync("xkirke");

        Assert.False((await settings.GetImportPlanAsync()).Backfill);
    }

    [Fact]
    public async Task A_failing_import_keeps_the_backfill_for_the_next_run()
    {
        var settings = new ChessCoach.Core.Settings.TimeClassSettings(new MemoryStore());
        await settings.SetImportClassesAsync(["bullet"]);
        var runner = Runner("import sys\nsys.exit(3)\n", settings);

        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync("xkirke"));

        Assert.True((await settings.GetImportPlanAsync()).Backfill);
    }

    [Fact]
    public async Task Without_time_class_settings_only_rapid_is_asked_for_and_no_backfill()
    {
        var runner = Runner("""
            import json, sys
            assert sys.argv[sys.argv.index("--time-classes") + 1] == "rapid", sys.argv
            assert "--backfill" not in sys.argv, sys.argv
            print(json.dumps({"stage": "done", "done": 1, "total": 1, "message": "Done."}), flush=True)
            """);

        await runner.RunAsync("xkirke");
    }
}
