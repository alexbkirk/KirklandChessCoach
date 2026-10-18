using System.IO.Compression;
using System.Security.Cryptography;
using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Setup;
using Microsoft.Extensions.Options;

namespace ChessCoach.Infrastructure.Setup;

/// <summary>
/// Downloads an engine, checks its size and SHA-256 against <see cref="EngineComponent"/>, and puts its file in the data
/// folder. A download that fails a check is deleted and never reaches the engine folder.
/// </summary>
public sealed class EngineInstaller(HttpClient http, IOptions<ChessCoachOptions> options) : IEngineInstaller
{
    private const int BufferSize = 81920;

    public async Task InstallAsync(EngineComponent component, IProgress<EngineProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var dataDirectory = options.Value.EffectiveDataDirectory;
        var engineFolder = Path.Combine(dataDirectory, "engine", component.FolderName);
        var downloads = Path.Combine(dataDirectory, "downloads");
        Directory.CreateDirectory(engineFolder);
        Directory.CreateDirectory(downloads);

        var download = Path.Combine(downloads, $"{component.FolderName}-{Guid.NewGuid():N}.part");
        try
        {
            await DownloadAsync(component, download, progress, cancellationToken);

            if (component.Delivery == EngineDelivery.File)
            {
                File.Move(download, Path.Combine(engineFolder, component.MainFile), overwrite: true);
            }
            else
            {
                ExtractZip(download, component, engineFolder);
            }

            if (!File.Exists(Path.Combine(engineFolder, component.MainFile)))
            {
                throw new InvalidDataException($"{component.Name}: {component.MainFile} was not in the download.");
            }
        }
        finally
        {
            if (File.Exists(download)) File.Delete(download);
        }
    }

    private async Task DownloadAsync(EngineComponent component, string path, IProgress<EngineProgress>? progress, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(component.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long done = 0;
        await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var target = File.Create(path))
        {
            var buffer = new byte[BufferSize];
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                hash.AppendData(buffer, 0, read);
                done += read;
                progress?.Report(new EngineProgress(component.Name, "Downloading", done, component.Size));
            }
        }

        if (done != component.Size)
        {
            throw new InvalidDataException($"{component.Name}: expected {component.Size:N0} bytes but downloaded {done:N0}. Please try again.");
        }

        var digest = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        if (!string.Equals(digest, component.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"{component.Name}: the download did not match its checksum, so it was discarded.");
        }

        progress?.Report(new EngineProgress(component.Name, "Checked", done, component.Size));
    }

    /// <summary>Keeps the wanted files from the archive, by file name only, so nothing is written outside the engine folder.</summary>
    private static void ExtractZip(string archive, EngineComponent component, string engineFolder)
    {
        using var zip = ZipFile.OpenRead(archive);
        foreach (var entry in zip.Entries)
        {
            var name = Path.GetFileName(entry.FullName);
            if (name.Length == 0) continue;   // a folder entry
            if (component.ZipEntries is not null && !component.ZipEntries.Contains(name, StringComparer.Ordinal)) continue;

            var staged = archive + "." + name;
            try
            {
                entry.ExtractToFile(staged, overwrite: true);
                File.Move(staged, Path.Combine(engineFolder, name), overwrite: true);
            }
            finally
            {
                if (File.Exists(staged)) File.Delete(staged);   // only left when extracting or moving failed
            }
        }
    }
}
