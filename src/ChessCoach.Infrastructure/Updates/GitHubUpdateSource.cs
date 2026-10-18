using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Updates;
using Microsoft.Extensions.Options;

namespace ChessCoach.Infrastructure.Updates;

/// <summary>
/// Reads the newest release of a public GitHub repository. A release counts only when it carries the installer
/// (<see cref="InstallerName"/>) and its checksum file (<see cref="InstallerName"/> + ".sha256"), both on github.com.
/// The checksum comes from the same release as the installer, so it catches a damaged or mismatched download, not a tampered release.
/// </summary>
public sealed partial class GitHubUpdateSource(HttpClient http, IOptions<ChessCoachOptions> options) : IUpdateSource
{
    public const string InstallerName = "ChessCoach-Setup.exe";

    /// <summary>A checksum file is one short line.</summary>
    private const int MaxChecksumBytes = 1024;

    // GitHub owners are letters, digits and hyphens; a repository may not be "." or "..", which would climb out of the API path
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9-]*/(?!\.{1,2}$)[A-Za-z0-9._-]+$")]
    private static partial Regex RepositoryPattern();

    // "<64 hex>" or the sha256sum form "<64 hex>  ChessCoach-Setup.exe", and nothing else
    [GeneratedRegex(@"^([0-9a-fA-F]{64})(?:[ \t]+\*?ChessCoach-Setup\.exe)?$")]
    private static partial Regex ChecksumPattern();

    public async Task<UpdateInfo?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        var repository = options.Value.UpdateRepository;
        if (!RepositoryPattern().IsMatch(repository))
        {
            throw new InvalidOperationException($"'{repository}' is not a GitHub repository name (owner/name).");
        }

        using var response = await TrustedDownload.GetAsync(
            http,
            new Uri($"https://api.github.com/repos/{repository}/releases/latest"),
            HttpCompletionOption.ResponseContentRead,
            cancellationToken,
            request =>
            {
                request.Headers.UserAgent.Add(new ProductInfoHeaderValue("ChessCoach", AppVersion.Current.ToString()));
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            });
        if (response.StatusCode == HttpStatusCode.NotFound) return null; // no release has been published yet
        response.EnsureSuccessStatusCode();

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var root = json.RootElement;

        var version = AppVersion.TryParse(root.TryGetProperty("tag_name", out var tag) ? tag.GetString() : null);
        if (version is null) return null;

        var installer = Asset(root, InstallerName);
        var checksum = Asset(root, InstallerName + ".sha256");
        if (installer is null || checksum is null) return null;

        var sha256 = await ReadChecksumAsync(checksum, cancellationToken);
        var notes = root.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "";
        return new UpdateInfo(version, notes, installer, sha256);
    }

    /// <summary>The release's own file links must be https downloads from github.com.</summary>
    public static bool IsTrustedUrl(Uri url) =>
        url.Scheme == Uri.UriSchemeHttps && url.IsDefaultPort && url.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase);

    private static Uri? Asset(JsonElement release, string name)
    {
        if (!release.TryGetProperty("assets", out var assets)) return null;
        foreach (var asset in assets.EnumerateArray())
        {
            if (asset.TryGetProperty("name", out var assetName) && assetName.GetString() == name
                && asset.TryGetProperty("browser_download_url", out var url)
                && Uri.TryCreate(url.GetString(), UriKind.Absolute, out var uri)
                && IsTrustedUrl(uri))
            {
                return uri;
            }
        }

        return null;
    }

    private async Task<string> ReadChecksumAsync(Uri url, CancellationToken cancellationToken)
    {
        using var response = await TrustedDownload.GetAsync(http, url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[MaxChecksumBytes + 1];
        var length = 0;
        int read;
        while (length < buffer.Length && (read = await stream.ReadAsync(buffer.AsMemory(length), cancellationToken)) > 0) length += read;
        if (length > MaxChecksumBytes) throw new InvalidDataException("The release's checksum file is too large to be a SHA-256.");

        var match = ChecksumPattern().Match(Encoding.UTF8.GetString(buffer, 0, length).Trim());
        return match.Success
            ? match.Groups[1].Value.ToLowerInvariant()
            : throw new InvalidDataException("The release's checksum file is not a SHA-256.");
    }
}
