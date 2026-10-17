namespace ChessCoach.Core.Abstractions;

/// <summary>
/// Keeps the AI coach's API key on this computer only: never in the database, appsettings.json or the repository.
/// </summary>
public interface IApiKeyStore
{
    /// <summary>The saved key, if any, and whether a saved file could not be decrypted.</summary>
    Task<StoredApiKey> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(string apiKey, CancellationToken cancellationToken = default);

    /// <summary>Deletes the saved key, readable or not. Does nothing when none is saved.</summary>
    Task RemoveAsync(CancellationToken cancellationToken = default);
}

public enum StoredApiKeyState
{
    None,
    Present,

    /// <summary>A key file exists but this Windows account cannot decrypt it (saved by another user or computer).</summary>
    Unreadable,
}

public sealed record StoredApiKey(StoredApiKeyState State, string? Key)
{
    public static StoredApiKey None { get; } = new(StoredApiKeyState.None, null);

    public static StoredApiKey Unreadable { get; } = new(StoredApiKeyState.Unreadable, null);

    public static StoredApiKey Present(string key) => new(StoredApiKeyState.Present, key);
}
