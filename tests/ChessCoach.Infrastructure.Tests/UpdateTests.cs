using System.Net;
using System.Security.Cryptography;
using System.Text;
using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Updates;
using ChessCoach.Infrastructure;
using ChessCoach.Infrastructure.Updates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ChessCoach.Infrastructure.Tests;

public sealed class UpdateTests : IDisposable
{
    private const string Download = "https://github.com/o/r/releases/download/v1.2.0/";
    private const string LatestUrl = "https://api.github.com/repos/o/r/releases/latest";
    private readonly string _dataDirectory = Path.Combine(Path.GetTempPath(), "chesscoach-update-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dataDirectory)) Directory.Delete(_dataDirectory, recursive: true);
    }

    /// <summary>Answers each URL from a table, so no test touches the network. Unknown URLs are 404. A 3xx status redirects to the body's text.</summary>
    private sealed class FakeHandler(Dictionary<string, (HttpStatusCode Status, byte[] Body)> responses) : HttpMessageHandler
    {
        public List<string> Requested { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            Requested.Add(url);
            var (status, body) = responses.GetValueOrDefault(url, (HttpStatusCode.NotFound, []));
            var response = new HttpResponseMessage(status) { Content = new ByteArrayContent(body) };
            if ((int)status is >= 301 and <= 308) response.Headers.Location = new Uri(Encoding.UTF8.GetString(body));
            return Task.FromResult(response);
        }
    }

    /// <summary>Wraps a handler and claims a Content-Length, so the size limit can be tested without a huge file.</summary>
    private sealed class LengthHandler(HttpMessageHandler inner, long length) : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            response.Content.Headers.ContentLength = length;
            return response;
        }
    }

    /// <summary>Records reports on the calling thread, unlike Progress&lt;T&gt;, so a test can read them straight away.</summary>
    private sealed class RecordingProgress : IProgress<double>
    {
        public List<double> Reports { get; } = [];

        public void Report(double value) => Reports.Add(value);
    }


    private static (HttpStatusCode, byte[]) Ok(string text) => (HttpStatusCode.OK, Encoding.UTF8.GetBytes(text));

    private static (HttpStatusCode, byte[]) Redirect(string location) => (HttpStatusCode.Found, Encoding.UTF8.GetBytes(location));

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

    private UpdateInstaller InstallerFor(FakeHandler handler) =>
        new(new HttpClient(handler), Options.Create(new ChessCoachOptions { DataDirectory = _dataDirectory }));

    private UpdateInstaller Installer(byte[] served, out FakeHandler handler)
    {
        handler = new FakeHandler(new() { [Download + "ChessCoach-Setup.exe"] = (HttpStatusCode.OK, served) });
        return InstallerFor(handler);
    }

    private static UpdateInfo Info(string sha, string url = Download + "ChessCoach-Setup.exe") => new(new Version(1, 2, 0), "", new Uri(url), sha);

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

    [Theory]
    [InlineData("oops")]
    [InlineData("cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc  other.exe")]
    [InlineData("cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc and then more")]
    [InlineData("cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc\nsecond line")]
    public async Task A_checksum_file_that_is_not_exactly_a_sha256_is_rejected(string content)
    {
        var source = Source(new()
        {
            [LatestUrl] = Ok(ReleaseJson()),
            [Download + "ChessCoach-Setup.exe.sha256"] = Ok(content),
        });

        await Assert.ThrowsAsync<InvalidDataException>(() => source.GetLatestAsync());
    }

    [Fact]
    public async Task A_checksum_file_far_bigger_than_a_checksum_is_rejected()
    {
        var source = Source(new()
        {
            [LatestUrl] = Ok(ReleaseJson()),
            [Download + "ChessCoach-Setup.exe.sha256"] = Ok(new string('c', 5000)),
        });

        await Assert.ThrowsAsync<InvalidDataException>(() => source.GetLatestAsync());
    }

    [Fact]
    public async Task A_renamed_repository_redirects_within_the_api_and_is_followed()
    {
        var source = Source(new()
        {
            [LatestUrl] = Redirect("https://api.github.com/repositories/42/releases/latest"),
            ["https://api.github.com/repositories/42/releases/latest"] = Ok(ReleaseJson()),
            [Download + "ChessCoach-Setup.exe.sha256"] = Ok(new string('c', 64)),
        });

        Assert.NotNull(await source.GetLatestAsync());
    }

    [Fact]
    public async Task The_release_lookup_refuses_a_redirect_to_another_host()
    {
        var handler = new FakeHandler(new() { [LatestUrl] = Redirect("https://evil.example/releases/latest") });
        var source = new GitHubUpdateSource(new HttpClient(handler), Options.Create(new ChessCoachOptions { UpdateRepository = "o/r" }));

        await Assert.ThrowsAsync<InvalidOperationException>(() => source.GetLatestAsync());

        Assert.DoesNotContain("https://evil.example/releases/latest", handler.Requested);
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

        Assert.Empty(Directory.GetFiles(Path.Combine(_dataDirectory, "downloads")));
    }

    [Fact]
    public async Task An_installer_outside_github_is_not_even_requested()
    {
        var installer = Installer([1], out var handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() => installer.DownloadAsync(Info(new string('0', 64), "https://evil.example/ChessCoach-Setup.exe")));

        Assert.Empty(handler.Requested);
    }

    [Fact]
    public async Task Progress_climbs_through_the_download_and_ends_at_one()
    {
        var bytes = new byte[200_000];
        Random.Shared.NextBytes(bytes);
        var installer = Installer(bytes, out _);
        var progress = new RecordingProgress();

        await installer.DownloadAsync(Info(Sha(bytes)), progress);

        Assert.Contains(progress.Reports, v => v is > 0 and < 1);
        Assert.Equal(1, progress.Reports[^1]);
        Assert.Equal(progress.Reports.Order(), progress.Reports);
    }

    [Fact]
    public async Task The_installer_is_followed_through_a_redirect_to_githubs_file_host()
    {
        var bytes = Encoding.UTF8.GetBytes("installer bytes");
        var installer = InstallerFor(new FakeHandler(new()
        {
            [Download + "ChessCoach-Setup.exe"] = Redirect("https://objects.githubusercontent.com/some/file"),
            ["https://objects.githubusercontent.com/some/file"] = (HttpStatusCode.OK, bytes),
        }));

        var path = await installer.DownloadAsync(Info(Sha(bytes)));

        Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
    }

    [Theory]
    [InlineData("https://evil.example/ChessCoach-Setup.exe")]
    [InlineData("http://objects.githubusercontent.com/some/file")]
    [InlineData("https://githubusercontent.com.evil.example/file")]
    public async Task A_redirect_to_anywhere_else_is_refused_and_not_contacted(string location)
    {
        var handler = new FakeHandler(new() { [Download + "ChessCoach-Setup.exe"] = Redirect(location) });
        var installer = InstallerFor(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() => installer.DownloadAsync(Info(new string('0', 64))));

        Assert.DoesNotContain(location, handler.Requested);
    }

    [Fact]
    public async Task A_redirect_loop_gives_up()
    {
        var installer = InstallerFor(new FakeHandler(new() { [Download + "ChessCoach-Setup.exe"] = Redirect(Download + "ChessCoach-Setup.exe") }));

        await Assert.ThrowsAsync<InvalidOperationException>(() => installer.DownloadAsync(Info(new string('0', 64))));
    }

    [Fact]
    public async Task An_installer_announced_as_far_too_large_is_not_downloaded()
    {
        var inner = new FakeHandler(new() { [Download + "ChessCoach-Setup.exe"] = (HttpStatusCode.OK, [1, 2, 3]) });
        var installer = new UpdateInstaller(
            new HttpClient(new LengthHandler(inner, UpdateInstaller.MaxInstallerBytes + 1)),
            Options.Create(new ChessCoachOptions { DataDirectory = _dataDirectory }));

        await Assert.ThrowsAsync<InvalidDataException>(() => installer.DownloadAsync(Info(Sha([1, 2, 3]))));

        Assert.Empty(Directory.GetFiles(Path.Combine(_dataDirectory, "downloads")));
    }

    [Fact]
    public async Task An_earlier_installer_is_cleared_before_the_next_download()
    {
        var bytes = Encoding.UTF8.GetBytes("installer bytes");
        var installer = Installer(bytes, out _);
        var downloads = Path.Combine(_dataDirectory, "downloads");
        Directory.CreateDirectory(downloads);
        var old = Path.Combine(downloads, "ChessCoach-Setup-1.0.0.exe");
        await File.WriteAllTextAsync(old, "old");

        await installer.DownloadAsync(Info(Sha(bytes)));

        Assert.False(File.Exists(old));
    }

    [Fact]
    public void The_update_pieces_are_registered_and_the_host_is_left_to_supply_the_lifecycle()
    {
        var services = new ServiceCollection();
        services.AddChessCoachInfrastructure(new ConfigurationBuilder().Build());

        // The host (WPF) registers IAppLifecycle; building the container here needs a package this test project does not use,
        // so resolution is checked by running the app.
        Assert.Contains(services, d => d.ServiceType == typeof(UpdateService));
        Assert.Contains(services, d => d.ServiceType == typeof(IUpdateSource));
        Assert.Contains(services, d => d.ServiceType == typeof(IUpdateInstaller));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IAppLifecycle));
    }
}
