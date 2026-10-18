using System.Net;
using System.Security.Cryptography;
using System.Text;
using ChessCoach.Core.Updates;
using ChessCoach.Infrastructure;
using ChessCoach.Infrastructure.Updates;
using Microsoft.Extensions.Options;

namespace ChessCoach.Infrastructure.Tests;

public sealed class UpdateTests : IDisposable
{
    private const string Download = "https://github.com/o/r/releases/download/v1.2.0/";
    private readonly string _dataDirectory = Path.Combine(Path.GetTempPath(), "chesscoach-update-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dataDirectory)) Directory.Delete(_dataDirectory, recursive: true);
    }

    /// <summary>Answers each URL from a table, so no test touches the network. Unknown URLs are 404.</summary>
    private sealed class FakeHandler(Dictionary<string, (HttpStatusCode Status, byte[] Body)> responses) : HttpMessageHandler
    {
        public List<string> Requested { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            Requested.Add(url);
            var (status, body) = responses.GetValueOrDefault(url, (HttpStatusCode.NotFound, []));
            return Task.FromResult(new HttpResponseMessage(status) { Content = new ByteArrayContent(body) });
        }
    }

    private static (HttpStatusCode, byte[]) Ok(string text) => (HttpStatusCode.OK, Encoding.UTF8.GetBytes(text));

    private static string ReleaseJson(string tag = "v1.2.0", bool installer = true, bool checksum = true, string host = "github.com", string notes = "Fixes things.")
    {
        var assets = new List<string>();
        if (installer) assets.Add($$"""{"name":"ChessCoach-Setup.exe","browser_download_url":"https://{{host}}/o/r/releases/download/v1.2.0/ChessCoach-Setup.exe"}""");
        if (checksum) assets.Add($$"""{"name":"ChessCoach-Setup.exe.sha256","browser_download_url":"https://{{host}}/o/r/releases/download/v1.2.0/ChessCoach-Setup.exe.sha256"}""");
        return $$"""{"tag_name":"{{tag}}","body":"{{notes}}","assets":[{{string.Join(",", assets)}}]}""";
    }

    private static string Sha(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static GitHubUpdateSource Source(Dictionary<string, (HttpStatusCode, byte[])> responses, string repository = "o/r") =>
        new(new HttpClient(new FakeHandler(responses)), Options.Create(new ChessCoachOptions { UpdateRepository = repository }));

    private const string LatestUrl = "https://api.github.com/repos/o/r/releases/latest";

    [Fact]
    public async Task The_latest_release_gives_its_version_notes_installer_and_checksum()
    {
        var hash = new string('c', 64);
        var source = Source(new()
        {
            [LatestUrl] = Ok(ReleaseJson()),
            [Download + "ChessCoach-Setup.exe.sha256"] = Ok($"{hash}  ChessCoach-Setup.exe\n"),
        });

        var update = await source.GetLatestAsync();

        Assert.NotNull(update);
        Assert.Equal(new Version(1, 2, 0), update.Version);
        Assert.Equal("Fixes things.", update.Notes);
        Assert.Equal(new Uri(Download + "ChessCoach-Setup.exe"), update.InstallerUrl);
        Assert.Equal(hash, update.Sha256);
    }

    [Fact]
    public async Task No_release_yet_is_not_an_error()
    {
        Assert.Null(await Source(new()).GetLatestAsync());
    }

    [Fact]
    public async Task A_server_error_is_thrown_so_the_check_reports_it()
    {
        var source = Source(new() { [LatestUrl] = (HttpStatusCode.InternalServerError, []) });

        await Assert.ThrowsAsync<HttpRequestException>(() => source.GetLatestAsync());
    }

    [Theory]
    [InlineData("not-a-version", true, true, "github.com")]
    [InlineData("v1.2.0", false, true, "github.com")]
    [InlineData("v1.2.0", true, false, "github.com")]
    [InlineData("v1.2.0", true, true, "evil.example")]
    public async Task A_release_without_a_usable_installer_is_not_offered(string tag, bool installer, bool checksum, string host)
    {
        var source = Source(new() { [LatestUrl] = Ok(ReleaseJson(tag, installer, checksum, host)) });

        Assert.Null(await source.GetLatestAsync());
    }

    [Fact]
    public async Task A_checksum_file_that_is_not_a_sha256_is_rejected()
    {
        var source = Source(new()
        {
            [LatestUrl] = Ok(ReleaseJson()),
            [Download + "ChessCoach-Setup.exe.sha256"] = Ok("oops"),
        });

        await Assert.ThrowsAsync<InvalidDataException>(() => source.GetLatestAsync());
    }

    [Theory]
    [InlineData("not a repo")]
    [InlineData("a/b/c")]
    [InlineData("../x")]
    [InlineData("o/..")]
    [InlineData("./x")]
    public async Task A_repository_name_that_is_not_owner_slash_name_is_refused_before_any_request(string repository)
    {
        var source = Source(new(), repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => source.GetLatestAsync());
    }

    [Theory]
    [InlineData("https://github.com/o/r/x.exe", true)]
    [InlineData("http://github.com/o/r/x.exe", false)]
    [InlineData("https://github.com.evil.example/x.exe", false)]
    [InlineData("https://evil.example/github.com/x.exe", false)]
    [InlineData("https://github.com:8443/x.exe", false)]
    public void Only_https_downloads_from_github_dot_com_are_trusted(string url, bool trusted)
    {
        Assert.Equal(trusted, GitHubUpdateSource.IsTrustedUrl(new Uri(url)));
    }

    private UpdateInstaller Installer(byte[] served, out FakeHandler handler)
    {
        handler = new FakeHandler(new() { [Download + "ChessCoach-Setup.exe"] = (HttpStatusCode.OK, served) });
        return new UpdateInstaller(new HttpClient(handler), Options.Create(new ChessCoachOptions { DataDirectory = _dataDirectory }));
    }

    private static UpdateInfo Info(string sha, string url = Download + "ChessCoach-Setup.exe") => new(new Version(1, 2, 0), "", new Uri(url), sha);

    [Fact]
    public async Task A_download_that_matches_its_checksum_is_saved_under_its_final_name()
    {
        var bytes = Encoding.UTF8.GetBytes("installer bytes");
        var installer = Installer(bytes, out _);

        var path = await installer.DownloadAsync(Info(Sha(bytes)));

        Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
        Assert.EndsWith("ChessCoach-Setup-1.2.0.exe", path);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.part"));
    }

    [Fact]
    public async Task A_download_that_does_not_match_is_deleted_and_never_reaches_its_final_name()
    {
        var installer = Installer(Encoding.UTF8.GetBytes("tampered"), out _);

        await Assert.ThrowsAsync<InvalidDataException>(() => installer.DownloadAsync(Info(new string('0', 64))));

        var downloads = Path.Combine(_dataDirectory, "downloads");
        Assert.Empty(Directory.GetFiles(downloads));
    }

    [Fact]
    public async Task An_installer_outside_github_is_not_even_requested()
    {
        var installer = Installer([1], out var handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() => installer.DownloadAsync(Info(new string('0', 64), "https://evil.example/ChessCoach-Setup.exe")));

        Assert.Empty(handler.Requested);
    }

    [Fact]
    public async Task Progress_ends_at_one()
    {
        var bytes = new byte[200_000];
        Random.Shared.NextBytes(bytes);
        var installer = Installer(bytes, out _);
        double last = 0;

        await installer.DownloadAsync(Info(Sha(bytes)), new Progress<double>(v => last = v));
        await Task.Delay(50); // Progress<T> reports on a thread-pool thread

        Assert.Equal(1, last);
    }
}
