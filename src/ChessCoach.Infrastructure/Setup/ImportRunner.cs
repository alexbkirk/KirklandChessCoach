using System.Diagnostics;
using System.Text.Json;
using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Settings;
using Microsoft.Extensions.Options;

namespace ChessCoach.Infrastructure.Setup;

/// <summary>
/// Runs analysis\refresh.py for a player, with the same Python the analysis always uses, and reports its progress.
/// The script writes one JSON object per progress line; anything else is kept only to explain a failure.
/// Cancelling stops the whole process tree. The script is resumable, so the next run carries on.
/// </summary>
public sealed class ImportRunner(IOptions<ChessCoachOptions> options, TimeClassSettings? timeClasses = null) : IImportRunner
{
    private const int KeptLines = 20;

    public async Task RunAsync(string username, IProgress<ImportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var python = settings.PythonPath ?? throw new InvalidOperationException("Python for the analysis was not found. Reinstall ChessCoach.");
        var analysis = settings.AnalysisDirectory ?? throw new InvalidOperationException("The analysis scripts were not found. Reinstall ChessCoach.");

        var start = new ProcessStartInfo(python)
        {
            WorkingDirectory = analysis,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("-I");
        start.ArgumentList.Add(Path.Combine(analysis, "refresh.py"));
        start.ArgumentList.Add("--data-dir");
        start.ArgumentList.Add(settings.EffectiveDataDirectory);
        start.ArgumentList.Add("--user");
        start.ArgumentList.Add(username);
        start.ArgumentList.Add("--progress");
        start.ArgumentList.Add("jsonl");

        // rapid always; blitz and bullet when the player turned them on. A class turned on since the last import needs every month searched.
        var plan = timeClasses is null ? new ImportPlan([TimeClasses.Rapid], Backfill: false) : await timeClasses.GetImportPlanAsync(cancellationToken);
        start.ArgumentList.Add("--time-classes");
        start.ArgumentList.Add(string.Join(',', plan.Classes));
        if (plan.Backfill) start.ArgumentList.Add("--backfill");

        using var process = Process.Start(start) ?? throw new InvalidOperationException("The analysis could not be started.");
        await using var stop = cancellationToken.Register(() => TryKill(process));

        var stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);
        var kept = new Queue<string>();
        string? line;
        while ((line = await process.StandardOutput.ReadLineAsync(CancellationToken.None)) is not null)
        {
            if (ParseProgress(line) is { } update)
            {
                progress?.Report(update);
            }
            else if (line.Length > 0)
            {
                kept.Enqueue(line);
                if (kept.Count > KeptLines) kept.Dequeue();
            }
        }

        await process.WaitForExitAsync(CancellationToken.None);
        cancellationToken.ThrowIfCancellationRequested();

        if (process.ExitCode != 0)
        {
            var errors = (await stderr).Trim();
            var tail = errors.Length > 0 ? errors : string.Join(Environment.NewLine, kept);
            throw new InvalidOperationException($"The import stopped with an error (exit code {process.ExitCode}).\n{tail}");
        }

        // the search of every month finished, so the next import can go back to looking only from the latest game
        if (plan.Backfill && timeClasses is not null) await timeClasses.ClearBackfillAsync(plan.Classes, CancellationToken.None);
    }

    /// <summary>One progress line as an update, or null when the line is not a progress object.</summary>
    internal static ImportProgress? ParseProgress(string line)
    {
        if (!line.StartsWith('{')) return null;
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (!root.TryGetProperty("stage", out var stage) || stage.ValueKind != JsonValueKind.String) return null;
            var done = root.TryGetProperty("done", out var d) && d.ValueKind == JsonValueKind.Number ? d.GetInt32() : 0;
            var total = root.TryGetProperty("total", out var t) && t.ValueKind == JsonValueKind.Number ? t.GetInt32() : 0;
            var message = root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() ?? "" : "";
            return new ImportProgress(stage.GetString()!, done, total, message);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // it finished on its own just now
        }
    }
}
