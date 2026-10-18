using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Updates;

namespace ChessCoach.Core.Tests;

public sealed class AppVersionTests
{
    [Theory]
    [InlineData("v1.2.0", "1.2.0")]
    [InlineData("1.2.0", "1.2.0")]
    [InlineData("V2.0.1", "2.0.1")]
    [InlineData("1.2", "1.2.0")]
    [InlineData("1.2.0.0", "1.2.0")]
    [InlineData(" v1.10.3 ", "1.10.3")]
    public void Release_tags_and_version_text_parse(string text, string expected)
    {
        Assert.Equal(Version.Parse(expected), AppVersion.TryParse(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("latest")]
    [InlineData("v")]
    [InlineData("1.x.0")]
    public void Anything_else_is_not_a_version(string? text)
    {
        Assert.Null(AppVersion.TryParse(text));
    }

    [Fact]
    public void Versions_compare_by_number_not_by_text()
    {
        Assert.True(AppVersion.TryParse("v1.10.0")! > AppVersion.TryParse("v1.9.0")!);
        Assert.Equal(AppVersion.Normalize(new Version(1, 0)), AppVersion.Normalize(new Version(1, 0, 0, 0)));
    }
}

public sealed class UpdateServiceTests
{
    private static readonly UpdateInfo Newer = new(new Version(1, 2, 0), "notes", new Uri("https://github.com/o/r/releases/download/v1.2.0/ChessCoach-Setup.exe"), new string('a', 64));

    private sealed class FakeSource(UpdateInfo? latest, Exception? error = null) : IUpdateSource
    {
        public int Calls { get; private set; }

        public Task<UpdateInfo?> GetLatestAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return error is null ? Task.FromResult(latest) : Task.FromException<UpdateInfo?>(error);
        }
    }

    private sealed class FakeInstaller(Exception? downloadError = null) : IUpdateInstaller
    {
        public string? Launched { get; private set; }

        public Task<string> DownloadAsync(UpdateInfo update, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            progress?.Report(1);
            return downloadError is null ? Task.FromResult(@"C:\temp\setup.exe") : Task.FromException<string>(downloadError);
        }

        public void Launch(string installerPath) => Launched = installerPath;
    }

    private sealed class FakeLifecycle : IAppLifecycle
    {
        public bool ShutDown { get; private set; }

        public void Shutdown() => ShutDown = true;
    }

    private static UpdateService Service(UpdateInfo? latest, string current = "1.0.0", Exception? sourceError = null, FakeInstaller? installer = null, FakeLifecycle? lifecycle = null) =>
        new(Version.Parse(current), new FakeSource(latest, sourceError), installer ?? new FakeInstaller(), lifecycle ?? new FakeLifecycle());

    [Fact]
    public async Task A_newer_release_makes_an_update_available()
    {
        var service = Service(Newer);

        await service.CheckAsync();

        Assert.Equal(UpdateState.Available, service.State);
        Assert.Equal(Newer, service.Available);
    }

    [Theory]
    [InlineData("1.2.0")]
    [InlineData("1.3.0")]
    public async Task The_same_or_a_newer_installed_version_is_up_to_date(string current)
    {
        var service = Service(Newer, current);

        await service.CheckAsync();

        Assert.Equal(UpdateState.UpToDate, service.State);
        Assert.Null(service.Available);
    }

    [Fact]
    public async Task No_release_yet_is_up_to_date()
    {
        var service = Service(null);

        await service.CheckAsync();

        Assert.Equal(UpdateState.UpToDate, service.State);
    }

    [Fact]
    public async Task A_failed_check_is_reported_and_does_not_throw()
    {
        var service = Service(null, sourceError: new HttpRequestException("offline"));

        await service.CheckAsync();

        Assert.Equal(UpdateState.Failed, service.State);
        Assert.Contains("offline", service.Error);
        Assert.Null(service.Available);
    }

    [Fact]
    public async Task Updating_downloads_launches_the_installer_and_closes_the_app()
    {
        var installer = new FakeInstaller();
        var lifecycle = new FakeLifecycle();
        var service = Service(Newer, installer: installer, lifecycle: lifecycle);
        await service.CheckAsync();

        await service.UpdateAsync();

        Assert.Equal(@"C:\temp\setup.exe", installer.Launched);
        Assert.True(lifecycle.ShutDown);
    }

    [Fact]
    public async Task Without_a_found_update_the_update_does_nothing()
    {
        var installer = new FakeInstaller();
        var lifecycle = new FakeLifecycle();
        var service = Service(Newer, "9.0.0", installer: installer, lifecycle: lifecycle);
        await service.CheckAsync();

        await service.UpdateAsync();

        Assert.Null(installer.Launched);
        Assert.False(lifecycle.ShutDown);
    }

    [Fact]
    public async Task A_failed_download_keeps_the_update_for_a_retry_and_leaves_the_app_open()
    {
        var installer = new FakeInstaller(new InvalidDataException("checksum"));
        var lifecycle = new FakeLifecycle();
        var service = Service(Newer, installer: installer, lifecycle: lifecycle);
        await service.CheckAsync();

        await service.UpdateAsync();

        Assert.Equal(UpdateState.Failed, service.State);
        Assert.Contains("checksum", service.Error);
        Assert.Equal(Newer, service.Available);
        Assert.Null(installer.Launched);
        Assert.False(lifecycle.ShutDown);
    }

    [Fact]
    public async Task Changed_is_raised_as_the_state_moves()
    {
        var service = Service(Newer);
        var raised = 0;
        service.Changed += () => raised++;

        await service.CheckAsync();

        Assert.Equal(2, raised);
    }
}
