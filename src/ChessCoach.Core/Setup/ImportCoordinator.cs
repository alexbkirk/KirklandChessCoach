using System.Globalization;
using ChessCoach.Core.Abstractions;

namespace ChessCoach.Core.Setup;

public enum ImportOutcome
{
    /// <summary>No import has finished since the app started.</summary>
    None,
    Finished,
    Paused,
    Failed,
}

/// <summary>
/// Runs the import for the whole app: the first import on the setup page and the later "check for new games" both go
/// through here, so only one runs at a time and its progress survives moving between pages.
/// The import is incremental, so running it again only fetches and analyses what is new.
/// </summary>
public sealed class ImportCoordinator(IImportRunner runner, ISetupService setup, IAppSettingsStore settings, IClock clock) : IDisposable
{
    public const string LastRefreshKey = "last_refresh_utc";

    private CancellationTokenSource? _cancel;

    /// <summary>Raised from a background thread whenever the state changes: marshal to the UI thread before using it.</summary>
    public event Action? Changed;

    public bool IsRunning { get; private set; }

    /// <summary>The latest progress update of the current or last run.</summary>
    public ImportProgress? Progress { get; private set; }

    /// <summary>The script's closing line of the last finished run, for example "Done. 3 new game(s) added; ...".</summary>
    public string? Result { get; private set; }

    public string? Error { get; private set; }

    public ImportOutcome Outcome { get; private set; }

    /// <summary>Completes when the current run ends. Tests await it; the UI follows <see cref="Changed"/> instead.</summary>
    public Task Completion { get; private set; } = Task.CompletedTask;

    /// <summary>
    /// Starts an import in the background. Returns false when one is already running.
    /// <paramref name="firstImport"/> records the first import's progress, so setup stays open until it has finished;
    /// a later check for new games never reopens setup.
    /// </summary>
    public bool Start(string username, bool firstImport)
    {
        lock (this)
        {
            if (IsRunning) return false;
            IsRunning = true;
        }

        Progress = null;
        Result = null;
        Error = null;
        Outcome = ImportOutcome.None;
        var cancel = _cancel = new CancellationTokenSource();
        Changed?.Invoke();
        Completion = RunAsync(username, firstImport, cancel);
        return true;
    }

    public void Pause()
    {
        try
        {
            _cancel?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // the run ended just now: nothing left to pause
        }
    }

    public void Dispose() => _cancel?.Dispose();

    /// <summary>When the last import finished, or null if none has since this data folder was set up.</summary>
    public async Task<DateTimeOffset?> GetLastRefreshAsync(CancellationToken cancellationToken = default)
    {
        var saved = await settings.GetAsync(LastRefreshKey, cancellationToken);
        return DateTimeOffset.TryParse(saved, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var when) ? when : null;
    }

    private async Task RunAsync(string username, bool firstImport, CancellationTokenSource cancel)
    {
        try
        {
            if (firstImport) await setup.MarkImportStartedAsync();
            await runner.RunAsync(username, new InlineProgress(Report), cancel.Token);
            if (firstImport) await setup.MarkImportFinishedAsync();
            await settings.SetAsync(LastRefreshKey, clock.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            Outcome = ImportOutcome.Finished;
        }
        catch (OperationCanceledException)
        {
            Outcome = ImportOutcome.Paused;
        }
        catch (Exception ex)
        {
            Outcome = ImportOutcome.Failed;
            Error = ex.Message;
        }
        finally
        {
            cancel.Dispose();
            _cancel = null;
            IsRunning = false;
            Changed?.Invoke();
        }
    }

    private void Report(ImportProgress progress)
    {
        Progress = progress;
        if (progress.Stage == "done") Result = progress.Message;
        Changed?.Invoke();
    }

    /// <summary>Delivers each update immediately, on the thread that reported it (Progress&lt;T&gt; would post them later).</summary>
    private sealed class InlineProgress(Action<ImportProgress> report) : IProgress<ImportProgress>
    {
        public void Report(ImportProgress value) => report(value);
    }
}

/// <summary>Plain-language labels for the import's stages, and for times.</summary>
public static class ImportText
{
    public static string StageLabel(string stage, string message) => stage switch
    {
        "fetch" => "Downloading your games from chess.com",
        "analyze" => "Analysing your games with Stockfish",
        "tag" => "Finding your blunders",
        "explain" => "Explaining the blunders",
        "coach" => "Writing coach notes",
        "done" => "Finished",
        _ => message,
    };

    /// <summary>"just now", "5 minutes ago", "3 hours ago", "2 days ago".</summary>
    public static string Ago(DateTimeOffset then, DateTimeOffset now)
    {
        var elapsed = now - then;
        if (elapsed < TimeSpan.FromMinutes(1)) return "just now";
        if (elapsed < TimeSpan.FromHours(1)) return Plural((int)elapsed.TotalMinutes, "minute");
        if (elapsed < TimeSpan.FromDays(1)) return Plural((int)elapsed.TotalHours, "hour");
        return Plural((int)elapsed.TotalDays, "day");
    }

    private static string Plural(int count, string unit) => $"{count.ToString(CultureInfo.InvariantCulture)} {unit}{(count == 1 ? "" : "s")} ago";
}
