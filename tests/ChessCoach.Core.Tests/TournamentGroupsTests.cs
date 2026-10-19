using ChessCoach.Core.Domain;
using ChessCoach.Core.Stats;

namespace ChessCoach.Core.Tests;

public sealed class TournamentGroupsTests
{
    private const string ChessComKey = "https://www.chess.com/tournament/live/100-rapid-819611";

    private static StatsGame Game(string id, string date = "2026-01-01", string time = "12:00:00") =>
        new(id, DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture), time, "white", "win", 1000, 1000, "600", "Open", "resignation", 40, "opp", "https://x");

    [Fact]
    public void NameOf_chess_com_url_uses_last_segment()
    {
        Assert.Equal("100 rapid 819611", TournamentGroups.NameOf(ChessComKey));
    }

    [Fact]
    public void NameOf_otb_key_uses_event_name()
    {
        Assert.Equal("Club Championship", TournamentGroups.NameOf("otb:Club Championship"));
    }

    [Fact]
    public void NameOf_empty_otb_key_is_unnamed_event()
    {
        Assert.Equal("Unnamed event", TournamentGroups.NameOf("otb:"));
    }

    [Fact]
    public void NameOf_old_over_the_board_value()
    {
        Assert.Equal("Over-the-board games", TournamentGroups.NameOf("over-the-board"));
    }

    [Fact]
    public void Two_tournaments_are_separated()
    {
        var games = new[] { Game("a"), Game("b"), Game("c") };
        var keys = new Dictionary<string, string>
        {
            ["a"] = ChessComKey,
            ["b"] = "otb:Club Championship",
            ["c"] = ChessComKey,
        };

        var result = TournamentGroups.Group(games, keys, []);

        Assert.Equal(2, result.Count);
        var chess = result.Single(t => t.Key == ChessComKey);
        Assert.False(chess.OverTheBoard);
        Assert.Equal(["a", "c"], chess.Games.Select(g => g.Id));
        var otb = result.Single(t => t.Key == "otb:Club Championship");
        Assert.True(otb.OverTheBoard);
        Assert.Equal(["b"], otb.Games.Select(g => g.Id));
    }

    [Fact]
    public void Groups_are_newest_first()
    {
        var games = new[]
        {
            Game("old", date: "2026-01-05"),
            Game("new", date: "2026-03-10"),
        };
        var keys = new Dictionary<string, string>
        {
            ["old"] = "otb:Early",
            ["new"] = "otb:Late",
        };

        var result = TournamentGroups.Group(games, keys, []);

        Assert.Equal(["Late", "Early"], result.Select(t => t.Name));
    }

    [Fact]
    public void Games_inside_a_group_are_oldest_first()
    {
        var games = new[]
        {
            Game("late", date: "2026-02-02", time: "10:00:00"),
            Game("early", date: "2026-02-01", time: "18:00:00"),
            Game("same-day-first", date: "2026-02-02", time: "09:00:00"),
        };
        var keys = games.ToDictionary(g => g.Id, _ => "otb:Event");

        var result = TournamentGroups.Group(games, keys, []);

        Assert.Single(result);
        Assert.Equal(["early", "same-day-first", "late"], result[0].Games.Select(g => g.Id));
        Assert.Equal(new DateOnly(2026, 2, 1), result[0].Start);
        Assert.Equal(new DateOnly(2026, 2, 2), result[0].End);
    }

    [Fact]
    public void Game_without_a_key_is_left_out()
    {
        var games = new[] { Game("keyed"), Game("unkeyed") };
        var keys = new Dictionary<string, string> { ["keyed"] = "otb:Event" };

        var result = TournamentGroups.Group(games, keys, []);

        Assert.Single(result);
        Assert.Equal(["keyed"], result[0].Games.Select(g => g.Id));
    }

    [Fact]
    public void Empty_input_gives_empty_list()
    {
        var result = TournamentGroups.Group([], new Dictionary<string, string>(), []);

        Assert.Empty(result);
    }
}
