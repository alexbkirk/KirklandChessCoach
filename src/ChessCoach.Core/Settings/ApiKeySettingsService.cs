using System.Security.Cryptography;
using ChessCoach.Core.Abstractions;

namespace ChessCoach.Core.Settings;

/// <summary>What the Settings page shows about the saved API key. Only the last 4 characters are ever exposed.</summary>
/// <param name="State">Whether a key is saved, and whether it can be read.</param>
/// <param name="Ending">Last 4 characters of a readable key; null otherwise.</param>
/// <param name="Message">Result of the last action, or the reason the key could not be read; null when there is nothing to say.</param>
/// <param name="Succeeded">True when the last save or remove went through.</param>
public sealed record ApiKeyView(StoredApiKeyState State, string? Ending, string? Message, bool Succeeded = false)
{
    public bool HasFile => State != StoredApiKeyState.None;
}

/// <summary>The Settings page's API key actions. File and encryption errors become messages instead of exceptions.</summary>
public sealed class ApiKeySettingsService(IApiKeyStore store)
{
    public const string KeyPrefix = "sk-ant-";

    public const string UnreadableMessage =
        "A saved key was found, but this Windows account can't read it (it was saved by another user or computer). Save your key again to replace it.";

    /// <summary>Whether a pasted key looks like an Anthropic key; used only to warn, never to refuse.</summary>
    public static bool LooksLikeAnthropicKey(string? key) =>
        string.IsNullOrWhiteSpace(key) || key.Trim().StartsWith(KeyPrefix, StringComparison.Ordinal);

    public async Task<ApiKeyView> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var stored = await store.GetAsync(cancellationToken);
            return stored.State switch
            {
                StoredApiKeyState.Present => new ApiKeyView(StoredApiKeyState.Present, Ending(stored.Key!), null),
                StoredApiKeyState.Unreadable => new ApiKeyView(StoredApiKeyState.Unreadable, null, UnreadableMessage),
                _ => new ApiKeyView(StoredApiKeyState.None, null, null),
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ApiKeyView(StoredApiKeyState.None, null, $"Could not read the saved key: {ex.Message}");
        }
    }

    public async Task<ApiKeyView> SaveAsync(string? apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return (await LoadAsync(cancellationToken)) with { Message = "Paste a key first." };
        }

        try
        {
            await store.SaveAsync(apiKey, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            return (await LoadAsync(cancellationToken)) with { Message = $"Could not save the key: {ex.Message}" };
        }

        return await WithMessageAsync("Saved.", cancellationToken);
    }

    public async Task<ApiKeyView> RemoveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await store.RemoveAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (await LoadAsync(cancellationToken)) with { Message = $"Could not remove the key: {ex.Message}" };
        }

        return await WithMessageAsync("Key removed.", cancellationToken);
    }

    /// <summary>The fresh state, keeping a read error if there is one, otherwise showing <paramref name="message"/>.</summary>
    private async Task<ApiKeyView> WithMessageAsync(string message, CancellationToken cancellationToken)
    {
        var view = await LoadAsync(cancellationToken) with { Succeeded = true };
        return view.Message is null ? view with { Message = message } : view;
    }

    private static string Ending(string key) => key.Length <= 4 ? key : key[^4..];
}
