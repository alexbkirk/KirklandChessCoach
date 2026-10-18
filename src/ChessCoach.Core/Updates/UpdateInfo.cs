namespace ChessCoach.Core.Updates;

/// <summary>A released version the app can be updated to.</summary>
/// <param name="Notes">The release notes, plain text.</param>
/// <param name="InstallerUrl">Where the installer is downloaded from.</param>
/// <param name="Sha256">The installer's SHA-256 (hex), which the download must match before it is run.</param>
public sealed record UpdateInfo(Version Version, string Notes, Uri InstallerUrl, string Sha256);
