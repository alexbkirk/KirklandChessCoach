using System.Runtime.Versioning;
using ChessCoach.Core.Abstractions;
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

        Assert.Equal(StoredApiKey.Present(FakeKey), await store.GetAsync());
        var bytes = await File.ReadAllBytesAsync(Path.Combine(_directory, DpapiApiKeyStore.FileName));
        Assert.DoesNotContain("test-key", System.Text.Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
    }

    [Fact]
    public async Task No_key_reads_as_null_and_remove_deletes_the_file()
    {
        var store = new DpapiApiKeyStore(Options.Create(new ChessCoachOptions { SecretsDirectory = _directory }));

        Assert.Equal(StoredApiKey.None, await store.GetAsync());
        await store.RemoveAsync();

        await store.SaveAsync(FakeKey);
        await store.RemoveAsync();

        Assert.Equal(StoredApiKey.None, await store.GetAsync());
        Assert.False(File.Exists(Path.Combine(_directory, DpapiApiKeyStore.FileName)));
    }

    [Fact]
    public async Task A_blank_key_is_rejected()
    {
        var store = new DpapiApiKeyStore(Options.Create(new ChessCoachOptions { SecretsDirectory = _directory }));

        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync("   "));
    }

    [Fact]
    public async Task A_file_that_cannot_be_decrypted_reads_as_unreadable_and_can_be_removed()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllBytesAsync(Path.Combine(_directory, DpapiApiKeyStore.FileName), [1, 2, 3, 4]);
        var store = new DpapiApiKeyStore(Options.Create(new ChessCoachOptions { SecretsDirectory = _directory }));

        Assert.Equal(StoredApiKey.Unreadable, await store.GetAsync());
        await store.RemoveAsync();
        Assert.Equal(StoredApiKey.None, await store.GetAsync());
    }

    [Fact]
    public async Task A_failed_save_leaves_no_temp_file_behind()
    {
        // A folder where the key file should be makes the final move fail.
        Directory.CreateDirectory(Path.Combine(_directory, DpapiApiKeyStore.FileName));
        var store = new DpapiApiKeyStore(Options.Create(new ChessCoachOptions { SecretsDirectory = _directory }));

        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(FakeKey));

        Assert.False(File.Exists(Path.Combine(_directory, DpapiApiKeyStore.FileName + ".tmp")));
    }
}
