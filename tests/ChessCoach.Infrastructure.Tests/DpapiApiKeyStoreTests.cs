using System.Runtime.Versioning;
using ChessCoach.Infrastructure.Secrets;
using Microsoft.Extensions.Options;

namespace ChessCoach.Infrastructure.Tests;

[SupportedOSPlatform("windows")]
public sealed class DpapiApiKeyStoreTests : IDisposable
{
    private const string FakeKey = "test-key-not-real-0123456789";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "chesscoach-secrets-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task A_saved_key_reads_back_and_is_not_stored_as_plain_text()
    {
        var store = new DpapiApiKeyStore(Options.Create(new ChessCoachOptions { SecretsDirectory = _directory }));

        await store.SaveAsync($"  {FakeKey}\n");

        Assert.Equal(FakeKey, await store.GetAsync());
        var bytes = await File.ReadAllBytesAsync(Path.Combine(_directory, DpapiApiKeyStore.FileName));
        Assert.DoesNotContain("test-key", System.Text.Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
    }

    [Fact]
    public async Task No_key_reads_as_null_and_remove_deletes_the_file()
    {
        var store = new DpapiApiKeyStore(Options.Create(new ChessCoachOptions { SecretsDirectory = _directory }));

        Assert.Null(await store.GetAsync());
        await store.RemoveAsync();

        await store.SaveAsync(FakeKey);
        await store.RemoveAsync();

        Assert.Null(await store.GetAsync());
        Assert.False(File.Exists(Path.Combine(_directory, DpapiApiKeyStore.FileName)));
    }

    [Fact]
    public async Task A_blank_key_is_rejected()
    {
        var store = new DpapiApiKeyStore(Options.Create(new ChessCoachOptions { SecretsDirectory = _directory }));

        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync("   "));
    }

    [Fact]
    public async Task A_file_that_cannot_be_decrypted_reads_as_no_key()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllBytesAsync(Path.Combine(_directory, DpapiApiKeyStore.FileName), [1, 2, 3, 4]);
        var store = new DpapiApiKeyStore(Options.Create(new ChessCoachOptions { SecretsDirectory = _directory }));

        Assert.Null(await store.GetAsync());
    }
}
