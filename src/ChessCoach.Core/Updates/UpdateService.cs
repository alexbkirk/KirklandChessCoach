using ChessCoach.Core.Abstractions;

namespace ChessCoach.Core.Updates;

public enum UpdateState
{
    /// <summary>No check has run yet.</summary>
    Idle,
    Checking,
    UpToDate,
    Available,
    Downloading,

    /// <summary>The last check or download failed; <see cref="UpdateService.Error"/> says why.</summary>
    Failed,
}

/// <summary>
/// Compares the running version with the newest release and, on request, downloads and starts its installer.
/// One instance for the whole app; the UI listens to <see cref="Changed"/>.
/// </summary>
public sealed class UpdateService(Version current, IUpdateSource source, IUpdateInstaller installer, IAppLifecycle lifecycle)
{
    private readonly Lock _gate = new();

    public Version Current { get; } = AppVersion.Normalize(current);

    public UpdateState State { get; private set; }

    /// <summary>The newer release, once one has been found. Kept after a failed download so it can be retried.</summary>
    public UpdateInfo? Available { get; private set; }

    /// <summary>Download progress from 0 to 1.</summary>
    public double Progress { get; private set; }

    public string? Error { get; private set; }

    public event Action? Changed;

    /// <summary>Looks for a newer release. Never throws: a failure (such as no internet) becomes the Failed state.</summary>
    public async Task CheckAsync(CancellationToken cancellationToken = default)
    {
        if (!Begin(UpdateState.Checking)) return;
        try
        {
            var latest = await source.GetLatestAsync(cancellationToken);
            if (latest is not null && AppVersion.Normalize(latest.Version) > Current)
            {
                Finish(UpdateState.Available, available: latest);
            }
            else
            {
                Finish(UpdateState.UpToDate, available: null);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Finish(UpdateState.Failed, available: CurrentAvailable(), error: $"Could not check for updates: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            var kept = CurrentAvailable();
            Finish(kept is null ? UpdateState.Idle : UpdateState.Available, available: kept);
            throw;
        }
    }

    /// <summary>Downloads the update, starts its installer and closes the app.</summary>
    public async Task UpdateAsync(CancellationToken cancellationToken = default)
    {
        var update = CurrentAvailable();
        if (update is null || !Begin(UpdateState.Downloading)) return;

        try
        {
            var progress = new Progress<double>(value =>
            {
                Progress = value;
                Changed?.Invoke();
            });
            var path = await installer.DownloadAsync(update, progress, cancellationToken);
            installer.Launch(path);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Finish(UpdateState.Failed, available: update, error: $"The update did not install: {ex.Message}");
            return;
        }
        catch (OperationCanceledException)
        {
            Finish(UpdateState.Available, available: update);
            throw;
        }

        // The installer is running now: a problem closing the app must not read as a failed update.
        try
        {
            lifecycle.Shutdown();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Finish(UpdateState.Failed, available: update, error: $"The installer has started, but ChessCoach could not close itself ({ex.Message}). Close ChessCoach to let the update finish.");
        }
    }

    private UpdateInfo? CurrentAvailable()
    {
        lock (_gate)
        {
            return Available;
        }
    }

    /// <summary>Moves to <paramref name="next"/> unless a check or download is already running.</summary>
    private bool Begin(UpdateState next)
    {
        lock (_gate)
        {
            if (State is UpdateState.Checking or UpdateState.Downloading) return false;
            State = next;
            Progress = 0;
            Error = null;
        }

        Changed?.Invoke();
        return true;
    }

    private void Finish(UpdateState next, UpdateInfo? available, string? error = null)
    {
        lock (_gate)
        {
            State = next;
            Available = available;
            Error = error;
        }

        Changed?.Invoke();
    }
}
