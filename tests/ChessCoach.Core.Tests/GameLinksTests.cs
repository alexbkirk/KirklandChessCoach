using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Tests;

public sealed class GameLinksTests
{
    [Theory]
    [InlineData("https://www.chess.com/game/live/94531334057", "94531334057")]
    [InlineData("https://www.chess.com/game/live/94531334057/", "94531334057")]
    [InlineData("https://www.chess.com/game/live/94531334057?move=5", "94531334057")]
    [InlineData("https://www.chess.com/game/live/", "")]
    [InlineData("https://example.com/", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("not a url", "")]
    public void The_game_id_is_the_numeric_last_part_of_the_address(string? url, string expected)
    {
        Assert.Equal(expected, GameLinks.IdFromUrl(url));
    }
}
