using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Setup;

namespace ChessCoach.Core.Tests;

public sealed class ImportCoordinatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private sealed class FakeRunner : IImportRunner
    {
        public TaskCompletionSource Gate { get; } = new();

        public bool Block { get; set; }

        public Exception? Fail { get; set; }

        public List<string> Users { get; } = [];

        public async Task RunAsync(string username, IProgress<ImportProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            Users.Add(username);
            progress?.Report(new ImportProgress("fetch", 1, 2, "Downloaded month 1 of 2"));
            if (Block) await Gate.Task.WaitAsync(cancellationToken);
            if (Fail is not null) throw Fail;
            progress?.Report(new ImportProgress("done", 1, 1, "Done. 3 new game(s) added; 1,411 games total."));
        }
    }

    private sealed class FakeSetup : ISetupService
    {
        public List<string> Calls { get; } = [];

        public Task<SetupStatus> GetStatusAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<string?> SaveUsernameAsync(string? username, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task MarkImportStartedAsync(CancellationToken cancellationToken = default) { Calls.Add("started"); return Task.CompletedTask; }

        public Task MarkImportFinishedAsync(CancellationToken cancellationToken = default) { Calls.Add("finished"); return Task.CompletedTask; }
    }

    private sealed class FakeSettings : IAppSettingsStore
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

    private static (ImportCoordinator Coordinator, FakeRunner Runner, FakeSetup Setup, FakeSettings Settings) Make() =>
        Create(new FakeRunner(), new FakeSetup(), new FakeSettings());

    private static (ImportCoordinator, FakeRunner, FakeSetup, FakeSettings) Create(FakeRunner runner, FakeSetup setup, FakeSettings settings) =>
        (new ImportCoordinator(runner, setup, settings, new FakeClock(Now)), runner, setup, settings);

    [Fact]
    public async Task A_refresh_runs_the_import_records_when_and_keeps_the_closing_message()
    {
        var (coordinator, runner, setup, settings) = Make();

        Assert.True(coordinator.Start("xkirke", firstImport: false));
        await coordinator.Completion;

        Assert.Equal(["xkirke"], runner.Users);
        Assert.Equal(ImportOutcome.Finished, coordinator.Outcome);
        Assert.False(coordinator.IsRunning);
        Assert.Equal("Done. 3 new game(s) added; 1,411 games total.", coordinator.Result);
        Assert.Equal(Now, await coordinator.GetLastRefreshAsync());
        Assert.Empty(setup.Calls);   // a refresh never reopens setup
        Assert.Contains(LastKey, settings.Values.Keys);
    }

    private const string LastKey = ImportCoordinator.LastRefreshKey;

    [Fact]
    public async Task The_first_import_marks_setup_started_then_finished()
    {
        var (coordinator, _, setup, _) = Make();

        coordinator.Start("xkirke", firstImport: true);
        await coordinator.Completion;

        Assert.Equal(["started", "finished"], setup.Calls);
    }

    [Fact]
    public async Task Only_one_import_runs_at_a_time()
    {
        var (coordinator, runner, _, _) = Make();
        runner.Block = true;

        Assert.True(coordinator.Start("xkirke", firstImport: false));
        Assert.True(coordinator.IsRunning);
        Assert.False(coordinator.Start("xkirke", firstImport: false));

        runner.Gate.SetResult();
        await coordinator.Completion;
        Assert.Single(runner.Users);
        Assert.True(coordinator.Start("xkirke", firstImport: false));   // free again once it ends
        await coordinator.Completion;
    }

    [Fact]
    public async Task Pausing_ends_the_run_without_recording_a_refresh_or_finishing_setup()
    {
        var (coordinator, runner, setup, settings) = Make();
        runner.Block = true;

        coordinator.Start("xkirke", firstImport: true);
        coordinator.Pause();
        await coordinator.Completion;

        Assert.Equal(ImportOutcome.Paused, coordinator.Outcome);
        Assert.False(coordinator.IsRunning);
        Assert.Equal(["started"], setup.Calls);
        Assert.Empty(settings.Values);
    }

    [Fact]
    public async Task Closing_the_app_stops_a_running_import_and_waits_for_it_to_end()
    {
        var (coordinator, runner, _, settings) = Make();
        runner.Block = true;
        coordinator.Start("xkirke", firstImport: false);
        Assert.True(coordinator.IsRunning);

        coordinator.Dispose();

        Assert.True(coordinator.Completion.IsCompleted);
        Assert.False(coordinator.IsRunning);
        Assert.Equal(ImportOutcome.Paused, coordinator.Outcome);
        Assert.Empty(settings.Values);   // stopped, so not recorded as a refresh
        await Task.CompletedTask;
    }

    [Fact]
    public void Closing_the_app_with_nothing_running_is_harmless()
    {
        var (coordinator, _, _, _) = Make();

        coordinator.Dispose();

        Assert.False(coordinator.IsRunning);
    }

    [Fact]
    public async Task A_failure_is_kept_as_the_error_and_does_not_count_as_a_refresh()
    {
        var (coordinator, runner, setup, settings) = Make();
        runner.Fail = new InvalidOperationException("chess.com could not be reached");

        coordinator.Start("xkirke", firstImport: true);
        await coordinator.Completion;

        Assert.Equal(ImportOutcome.Failed, coordinator.Outcome);
        Assert.Equal("chess.com could not be reached", coordinator.Error);
        Assert.Equal(["started"], setup.Calls);
        Assert.Null(await coordinator.GetLastRefreshAsync());
        Assert.Empty(settings.Values);
    }

    [Fact]
    public async Task Changes_are_announced_as_the_run_starts_progresses_and_ends()
    {
        var (coordinator, _, _, _) = Make();
        var running = new List<bool>();
        coordinator.Changed += () => running.Add(coordinator.IsRunning);

        coordinator.Start("xkirke", firstImport: false);
        await coordinator.Completion;

        Assert.True(running.First());
        Assert.False(running.Last());
        Assert.Equal(new ImportProgress("done", 1, 1, "Done. 3 new game(s) added; 1,411 games total."), coordinator.Progress);
    }

    [Fact]
    public async Task A_garbled_saved_time_reads_as_never()
    {
        var (coordinator, _, _, settings) = Make();
        settings.Values[LastKey] = "yesterday-ish";

        Assert.Null(await coordinator.GetLastRefreshAsync());
    }

    [Theory]
    [InlineData(0, "just now")]
    [InlineData(59, "just now")]
    [InlineData(60, "1 minute ago")]
    [InlineData(5 * 60, "5 minutes ago")]
    [InlineData(60 * 60, "1 hour ago")]
    [InlineData(3 * 3600 + 120, "3 hours ago")]
    [InlineData(24 * 3600, "1 day ago")]
    [InlineData(5 * 86400, "5 days ago")]
    [InlineData(-30, "just now")]
    public void Times_read_in_plain_language(int secondsAgo, string expected)
    {
        Assert.Equal(expected, ImportText.Ago(Now.AddSeconds(-secondsAgo), Now));
    }

    [Theory]
    [InlineData("fetch", "Downloading your games from chess.com")]
    [InlineData("analyze", "Analysing your games with Stockfish")]
    [InlineData("done", "Finished")]
    [InlineData("something-new", "the script's own words")]
    public void Stages_have_plain_labels_and_unknown_ones_show_the_scripts_message(string stage, string label)
    {
        Assert.Equal(label, ImportText.StageLabel(stage, "the script's own words"));
    }
}
