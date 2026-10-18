using System.Diagnostics;
using System.Security.Cryptography;
using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Updates;
using Microsoft.Extensions.Options;

namespace ChessCoach.Infrastructure.Updates;

/// <summary>
/// Downloads the installer into the data folder's downloads folder and runs it. The file is only given its final name once its
/// SHA-256 matches the release's, so a download that fails the check can never be started.
/// </summary>
public sealed class UpdateInstaller(HttpClient http, IOptions<ChessCoachOptions> options) : IUpdateInstaller
{
    private const int BufferSize = 81920;

    public async Task<string> DownloadAsync(UpdateInfo update, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!GitHubUpdateSource.IsTrustedUrl(update.InstallerUrl))
        {
            throw new InvalidOperationException("The installer is not on github.com, so it was not downloaded.");
        }

        var downloads = Path.Combine(options.Value.EffectiveDataDirectory, "downloads");
        Directory.CreateDirectory(downloads);
        var part = Path.Combine(downloads, $"ChessCoach-Setup-{update.Version}-{Guid.NewGuid():N}.part");
        var final = Path.Combine(downloads, $"ChessCoach-Setup-{update.Version}.exe");

        try
        {
            string actual;
            using (var response = await http.GetAsync(update.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength;
                await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var target = new FileStream(part, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

                var buffer = new byte[BufferSize];
                long written = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    hash.AppendData(buffer, 0, read);
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    written += read;
                    if (total is > 0) progress?.Report(Math.Min(1, (double)written / total.Value));
                }

                actual = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            }

            if (!string.Equals(actual, update.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The downloaded installer did not match its checksum, so it was not run.");
            }

            File.Move(part, final, overwrite: true);
            progress?.Report(1);
            return final;
        }
        finally
        {
            if (File.Exists(part)) File.Delete(part);
        }
    }

    public void Launch(string installerPath)
    {
        // The shell starts it, so Windows can ask for permission if the install needs it.
        Process.Start(new ProcessStartInfo(installerPath) { UseShellExecute = true });
    }
}
