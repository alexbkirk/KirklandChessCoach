using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Setup;
using Microsoft.Extensions.Options;

namespace ChessCoach.Infrastructure.Setup;

/// <summary>Works out the setup status from the database and the data folder, and saves the chosen username.</summary>
public sealed class SetupService(IAppSettingsStore settings, IGameRepository games, IOptions<ChessCoachOptions> options) : ISetupService
{
    public const string UsernameKey = "chess_com_username";

    public async Task<SetupStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var username = await settings.GetAsync(UsernameKey, cancellationToken);
        var dataDirectory = options.Value.EffectiveDataDirectory;
        var missing = EngineCatalog.Missing(path => File.Exists(Path.Combine(dataDirectory, path)));
        var summary = await games.GetSummaryAsync(cancellationToken);
        return new SetupStatus(string.IsNullOrWhiteSpace(username) ? null : username, missing, summary.GameCount);
    }

    public async Task<string?> SaveUsernameAsync(string? username, CancellationToken cancellationToken = default)
    {
        if (!ChessComUsername.IsValid(username))
        {
            return "Enter the username exactly as it appears on chess.com: 3 to 25 letters, numbers, _ or -.";
        }

        await settings.SetAsync(UsernameKey, username!.Trim(), cancellationToken);
        return null;
    }
}
