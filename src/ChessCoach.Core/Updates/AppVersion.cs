using System.Reflection;

namespace ChessCoach.Core.Updates;

/// <summary>Reads and compares release versions such as <c>v1.2.0</c>. Only major.minor.patch counts.</summary>
public static class AppVersion
{
    /// <summary>The version of the running app (the entry assembly), or 0.0.0 when it cannot be read.</summary>
    public static Version Current => Normalize(Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0));

    /// <summary>Parses a release tag or version text: "v1.2.0", "1.2" and "1.2.0.0" are all accepted; anything else is null.</summary>
    public static Version? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var trimmed = text.Trim();
        if (trimmed.StartsWith('v') || trimmed.StartsWith('V')) trimmed = trimmed[1..];
        return Version.TryParse(trimmed, out var version) ? Normalize(version) : null;
    }

    /// <summary>Keeps major, minor and patch, so 1.0 and 1.0.0.0 are the same version.</summary>
    public static Version Normalize(Version version) => new(version.Major, version.Minor, Math.Max(version.Build, 0));
}
