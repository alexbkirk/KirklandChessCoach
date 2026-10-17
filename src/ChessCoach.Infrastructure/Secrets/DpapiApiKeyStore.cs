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

    public async Task<string?> GetAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(FilePath)) return null;
        var encrypted = await File.ReadAllBytesAsync(FilePath, cancellationToken);
        try
        {
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser));
        }
        catch (CryptographicException)
        {
            return null;   // saved by another Windows user or computer: treat as no key
        }
    }

    public async Task SaveAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        var trimmed = apiKey?.Trim();
        ArgumentException.ThrowIfNullOrEmpty(trimmed, nameof(apiKey));
        Directory.CreateDirectory(options.Value.SecretsDirectory);
        var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(trimmed), Entropy, DataProtectionScope.CurrentUser);
        var temp = FilePath + ".tmp";
        await File.WriteAllBytesAsync(temp, encrypted, cancellationToken);
        File.Move(temp, FilePath, overwrite: true);
    }

    public Task RemoveAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(FilePath)) File.Delete(FilePath);
        return Task.CompletedTask;
    }
}
