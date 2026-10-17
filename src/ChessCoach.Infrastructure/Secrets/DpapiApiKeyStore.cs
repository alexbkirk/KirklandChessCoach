using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using ChessCoach.Core.Abstractions;
using Microsoft.Extensions.Options;

namespace ChessCoach.Infrastructure.Secrets;

/// <summary>
/// Saves the API key in <see cref="ChessCoachOptions.SecretsDirectory"/> (by default %LOCALAPPDATA%\ChessCoach),
/// encrypted with Windows DPAPI for the current user: only this Windows account on this computer can read it back.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiApiKeyStore(IOptions<ChessCoachOptions> options) : IApiKeyStore
{
    public const string FileName = "anthropic-api-key.bin";

    // Extra entropy ties the file to this app's format. It is not a secret (it is in the repository): DPAPI protects the key
    // from other Windows users and other computers, not from other programs running as this user.
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ChessCoach/anthropic-api-key/v1");

    private string FilePath => Path.Combine(options.Value.SecretsDirectory, FileName);

    public async Task<StoredApiKey> GetAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(FilePath)) return StoredApiKey.None;
        var encrypted = await File.ReadAllBytesAsync(FilePath, cancellationToken);
        try
        {
            return StoredApiKey.Present(Encoding.UTF8.GetString(ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser)));
        }
        catch (CryptographicException)
        {
            return StoredApiKey.Unreadable;   // saved by another Windows user or computer, or damaged
        }
    }

    public async Task SaveAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        var trimmed = apiKey?.Trim();
        ArgumentException.ThrowIfNullOrEmpty(trimmed, nameof(apiKey));
        Directory.CreateDirectory(options.Value.SecretsDirectory);
        var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(trimmed), Entropy, DataProtectionScope.CurrentUser);
        var temp = FilePath + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temp, encrypted, cancellationToken);
            File.Move(temp, FilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);   // only left behind when the write or move failed
        }
    }

    public Task RemoveAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(FilePath)) File.Delete(FilePath);
        return Task.CompletedTask;
    }
}
