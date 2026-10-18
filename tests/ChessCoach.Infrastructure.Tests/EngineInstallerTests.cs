using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using ChessCoach.Core.Setup;
using ChessCoach.Infrastructure;
using ChessCoach.Infrastructure.Setup;
using Microsoft.Extensions.Options;

namespace ChessCoach.Infrastructure.Tests;

public sealed class EngineInstallerTests : IDisposable
{
    private readonly string _dataDirectory = Path.Combine(Path.GetTempPath(), "chesscoach-engines-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dataDirectory)) Directory.Delete(_dataDirectory, recursive: true);
    }

    /// <summary>Serves fixed bytes for any request, so no test touches the network.</summary>
    private sealed class FixedBytesHandler(byte[] bytes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
    }

    private EngineInstaller Installer(byte[] served) =>
        new(new HttpClient(new FixedBytesHandler(served)), Options.Create(new ChessCoachOptions { DataDirectory = _dataDirectory }));

    private static byte[] Zip(params (string Path, string Text)[] files)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, text) in files)
            {
                using var writer = new StreamWriter(zip.CreateEntry(path).Open(), Encoding.UTF8);
                writer.Write(text);
            }
        }

        return stream.ToArray();
    }

    private static EngineComponent Component(byte[] served, EngineDelivery delivery, string mainFile, IReadOnlyList<string>? keep = null, long? size = null, string? sha = null) =>
        new(
            Name: "Test engine",
            Description: "test",
            Url: "https://example.invalid/engine",
            Sha256: sha ?? Convert.ToHexString(SHA256.HashData(served)).ToLowerInvariant(),
            Size: size ?? served.Length,
            Delivery: delivery,
            FolderName: "test",
            MainFile: mainFile,
            ZipEntries: keep);

    private string EngineFolder => Path.Combine(_dataDirectory, "engine", "test");

    [Fact]
    public async Task A_zip_keeps_only_the_wanted_files_flattened_into_the_engine_folder()
    {
        var served = Zip(("dir/program.exe", "binary"), ("dir/src/notes.txt", "source"), ("dir/readme.md", "read me"));

        await Installer(served).InstallAsync(Component(served, EngineDelivery.Zip, "program.exe", keep: ["program.exe"]));

        Assert.Equal("binary", await File.ReadAllTextAsync(Path.Combine(EngineFolder, "program.exe")));
        Assert.False(File.Exists(Path.Combine(EngineFolder, "notes.txt")));
        Assert.False(File.Exists(Path.Combine(EngineFolder, "readme.md")));
    }

    [Fact]
    public async Task A_zip_without_a_filter_keeps_every_file()
    {
        var served = Zip(("lc0.exe", "binary"), ("dnnl.dll", "library"));

        await Installer(served).InstallAsync(Component(served, EngineDelivery.Zip, "lc0.exe"));

        Assert.Equal("library", await File.ReadAllTextAsync(Path.Combine(EngineFolder, "dnnl.dll")));
    }

    [Fact]
    public async Task A_single_file_lands_at_its_main_file_name()
    {
        var served = Encoding.UTF8.GetBytes("weights");

        await Installer(served).InstallAsync(Component(served, EngineDelivery.File, "weights.pb.gz"));

        Assert.Equal("weights", await File.ReadAllTextAsync(Path.Combine(EngineFolder, "weights.pb.gz")));
    }

    [Fact]
    public async Task A_download_with_the_wrong_checksum_is_refused_and_leaves_nothing_behind()
    {
        var served = Encoding.UTF8.GetBytes("weights");
        var wrong = Component(served, EngineDelivery.File, "weights.pb.gz", sha: new string('0', 64));

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => Installer(served).InstallAsync(wrong));

        Assert.Contains("checksum", error.Message);
        Assert.False(File.Exists(Path.Combine(EngineFolder, "weights.pb.gz")));
        Assert.Empty(Directory.GetFiles(Path.Combine(_dataDirectory, "downloads")));
    }

    [Fact]
    public async Task A_download_of_the_wrong_size_is_refused()
    {
        var served = Encoding.UTF8.GetBytes("weights");
        var wrong = Component(served, EngineDelivery.File, "weights.pb.gz", size: served.Length + 1);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => Installer(served).InstallAsync(wrong));

        Assert.Contains("expected", error.Message);
        Assert.False(File.Exists(Path.Combine(EngineFolder, "weights.pb.gz")));
    }

    [Fact]
    public async Task A_zip_missing_its_main_file_is_reported()
    {
        var served = Zip(("other.exe", "binary"));

        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            Installer(served).InstallAsync(Component(served, EngineDelivery.Zip, "program.exe", keep: ["program.exe"])));

        Assert.Contains("program.exe was not in the download", error.Message);
    }

    [Fact]
    public async Task Progress_is_reported_for_the_download_and_the_check()
    {
        var served = Encoding.UTF8.GetBytes("weights");
        var reports = new List<EngineProgress>();

        await Installer(served).InstallAsync(Component(served, EngineDelivery.File, "weights.pb.gz"), new Progress<EngineProgress>(reports.Add));

        await Task.Yield();   // Progress<T> posts its callbacks, so give them a chance to run
        Assert.Contains(reports, r => r.Stage == "Downloading" && r.Done == served.Length);
        Assert.Contains(reports, r => r.Stage == "Checked");
    }
}
