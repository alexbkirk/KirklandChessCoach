namespace ChessCoach.Core.Abstractions;

/// <summary>
/// Keeps the AI coach's API key on this computer only: never in the database, appsettings.json or the repository.
/// </summary>
public interface IApiKeyStore
{
    /// <summary>The saved key, or null when none is saved.</summary>
    Task<string?> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(string apiKey, CancellationToken cancellationToken = default);

    /// <summary>Deletes the saved key. Does nothing when none is saved.</summary>
    Task RemoveAsync(CancellationToken cancellationToken = default);
}
