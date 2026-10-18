namespace ChessCoach.Core.Abstractions;

/// <summary>Small preferences the app keeps in its database (table app_settings), such as the chosen theme.</summary>
public interface IAppSettingsStore
{
    /// <summary>The saved value, or null when the key has never been set.</summary>
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    Task SetAsync(string key, string value, CancellationToken cancellationToken = default);
}
