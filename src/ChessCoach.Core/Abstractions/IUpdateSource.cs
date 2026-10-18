using ChessCoach.Core.Updates;

namespace ChessCoach.Core.Abstractions;

/// <summary>Where the newest release comes from.</summary>
public interface IUpdateSource
{
    /// <summary>The newest installable release, or null when there is none yet.</summary>
    Task<UpdateInfo?> GetLatestAsync(CancellationToken cancellationToken = default);
}

/// <summary>Downloads an installer and runs it.</summary>
public interface IUpdateInstaller
{
    /// <summary>Downloads the installer, checks it against <see cref="UpdateInfo.Sha256"/>, and returns its path. Throws when the check fails.</summary>
    Task<string> DownloadAsync(UpdateInfo update, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>Starts the downloaded installer.</summary>
    void Launch(string installerPath);
}

/// <summary>Lets the update flow close the app so the installer can replace its files.</summary>
public interface IAppLifecycle
{
    void Shutdown();
}
