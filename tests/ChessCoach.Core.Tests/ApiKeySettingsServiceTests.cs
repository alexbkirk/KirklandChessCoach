using System.Security.Cryptography;
using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Settings;

namespace ChessCoach.Core.Tests;

public sealed class ApiKeySettingsServiceTests
{
    private const string FakeKey = "sk-ant-test-not-real-WXYZ";

    private sealed class FakeStore : IApiKeyStore
    {
        public StoredApiKey Stored { get; set; } = StoredApiKey.None;

        public Exception? ThrowOnGet { get; set; }

        public Exception? ThrowOnSave { get; set; }

        public Exception? ThrowOnRemove { get; set; }

        public Task<StoredApiKey> GetAsync(CancellationToken cancellationToken = default) =>
            ThrowOnGet is null ? Task.FromResult(Stored) : Task.FromException<StoredApiKey>(ThrowOnGet);

        public Task SaveAsync(string apiKey, CancellationToken cancellationToken = default)
        {
            if (ThrowOnSave is not null) return Task.FromException(ThrowOnSave);
            Stored = StoredApiKey.Present(apiKey.Trim());
            return Task.CompletedTask;
        }

        public Task RemoveAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowOnRemove is not null) return Task.FromException(ThrowOnRemove);
            Stored = StoredApiKey.None;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task A_saved_key_shows_only_its_last_four_characters()
    {
        var store = new FakeStore { Stored = StoredApiKey.Present(FakeKey) };

        var view = await new ApiKeySettingsService(store).LoadAsync();

        Assert.Equal(StoredApiKeyState.Present, view.State);
        Assert.Equal("WXYZ", view.Ending);
        Assert.Null(view.Message);
    }

    [Fact]
    public async Task An_unreadable_key_says_so_instead_of_looking_like_no_key()
    {
        var store = new FakeStore { Stored = StoredApiKey.Unreadable };

        var view = await new ApiKeySettingsService(store).LoadAsync();

        Assert.Equal(StoredApiKeyState.Unreadable, view.State);
        Assert.True(view.HasFile);
        Assert.Null(view.Ending);
        Assert.Equal(ApiKeySettingsService.UnreadableMessage, view.Message);
    }

    [Fact]
    public async Task A_read_error_becomes_a_message()
    {
        var store = new FakeStore { ThrowOnGet = new IOException("file is locked") };

        var view = await new ApiKeySettingsService(store).LoadAsync();

        Assert.Equal(StoredApiKeyState.None, view.State);
        Assert.Equal("Could not read the saved key: file is locked", view.Message);
    }

    [Fact]
    public async Task Saving_stores_the_key_and_reports_success()
    {
        var store = new FakeStore();

        var view = await new ApiKeySettingsService(store).SaveAsync($" {FakeKey} ");

        Assert.True(view.Succeeded);
        Assert.Equal("Saved.", view.Message);
        Assert.Equal("WXYZ", view.Ending);
        Assert.Equal(FakeKey, store.Stored.Key);
    }

    [Fact]
    public async Task A_blank_key_is_not_saved()
    {
        var store = new FakeStore();

        var view = await new ApiKeySettingsService(store).SaveAsync("   ");

        Assert.False(view.Succeeded);
        Assert.Equal("Paste a key first.", view.Message);
        Assert.Equal(StoredApiKeyState.None, store.Stored.State);
    }

    [Theory]
    [InlineData("access denied", typeof(UnauthorizedAccessException))]
    [InlineData("disk full", typeof(IOException))]
    [InlineData("dpapi failed", typeof(CryptographicException))]
    public async Task A_save_error_becomes_a_message_and_keeps_the_old_key(string reason, Type exceptionType)
    {
        var store = new FakeStore { Stored = StoredApiKey.Present("sk-ant-old-key-ABCD"), ThrowOnSave = (Exception)Activator.CreateInstance(exceptionType, reason)! };

        var view = await new ApiKeySettingsService(store).SaveAsync(FakeKey);

        Assert.False(view.Succeeded);
        Assert.Equal($"Could not save the key: {reason}", view.Message);
        Assert.Equal("ABCD", view.Ending);
    }

    [Fact]
    public async Task Removing_clears_the_key()
    {
        var store = new FakeStore { Stored = StoredApiKey.Present(FakeKey) };

        var view = await new ApiKeySettingsService(store).RemoveAsync();

        Assert.True(view.Succeeded);
        Assert.Equal("Key removed.", view.Message);
        Assert.Equal(StoredApiKeyState.None, view.State);
    }

    [Fact]
    public async Task A_remove_error_becomes_a_message_and_the_key_stays()
    {
        var store = new FakeStore { Stored = StoredApiKey.Present(FakeKey), ThrowOnRemove = new UnauthorizedAccessException("access denied") };

        var view = await new ApiKeySettingsService(store).RemoveAsync();

        Assert.False(view.Succeeded);
        Assert.Equal("Could not remove the key: access denied", view.Message);
        Assert.Equal(StoredApiKeyState.Present, view.State);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("  sk-ant-abc", true)]
    [InlineData("abc-sk-ant", false)]
    public void Only_keys_without_the_anthropic_prefix_get_a_warning(string? key, bool looksRight)
    {
        Assert.Equal(looksRight, ApiKeySettingsService.LooksLikeAnthropicKey(key));
    }
}
