namespace ChessCoach.Core.Domain;

public static class GameLinks
{
    /// <summary>
    /// The game id inside a chess.com game address (its last path part, which is all digits), or "" when the address has none.
    /// A query string or trailing slash is ignored.
    /// </summary>
    public static string IdFromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri)) return "";
        var last = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "";
        return last.Length > 0 && last.All(char.IsAsciiDigit) ? last : "";
    }
}
