using System.Text.RegularExpressions;
using ChessCoach.Core.Domain;

namespace ChessCoach.Core.Tests;

public sealed class ManualGameTests
{
    private static readonly DateOnly Day = new(2026, 10, 19);

    private static ManualGame Game(
        string myColor = "white",
        string result = "win",
        IReadOnlyList<string>? sans = null,
        string opponent = "Alex",
        int myRating = 1500,
        int oppRating = 1600) =>
        new(Day, myColor, opponent, myRating, oppRating, result, "Club night", sans ?? ["e4", "e5", "Nf3"]);

    private static string MovetextOf(string pgn) => pgn.Split("\n\n")[1];

    [Fact]
    public void Colours_map_to_White_and_Black_tags()
    {
        var asWhite = Game(myColor: "white").Pgn;
        var asBlack = Game(myColor: "black").Pgn;

        Assert.Contains("[White \"Me\"]", asWhite);
        Assert.Contains("[Black \"Alex\"]", asWhite);
        Assert.Contains("[WhiteElo \"1500\"]", asWhite);
        Assert.Contains("[BlackElo \"1600\"]", asWhite);

        Assert.Contains("[White \"Alex\"]", asBlack);
        Assert.Contains("[Black \"Me\"]", asBlack);
        Assert.Contains("[WhiteElo \"1600\"]", asBlack);
        Assert.Contains("[BlackElo \"1500\"]", asBlack);
    }

    [Theory]
    [InlineData("white", "win", "1-0")]
    [InlineData("white", "loss", "0-1")]
    [InlineData("white", "draw", "1/2-1/2")]
    [InlineData("black", "win", "0-1")]
    [InlineData("black", "loss", "1-0")]
    [InlineData("black", "draw", "1/2-1/2")]
    public void Results_map_to_PGN_results_for_both_colours(string myColor, string result, string expected)
    {
        var pgn = Game(myColor: myColor, result: result).Pgn;

        Assert.Contains($"[Result \"{expected}\"]", pgn);
        Assert.EndsWith(" " + expected, MovetextOf(pgn));
    }

    [Fact]
    public void Move_numbers_are_right_for_an_odd_number_of_plies()
    {
        var pgn = Game(sans: ["e4", "e5", "Nf3"], result: "win").Pgn;

        Assert.Equal("1. e4 e5 2. Nf3 1-0", MovetextOf(pgn));
    }

    [Fact]
    public void Move_numbers_are_right_for_an_even_number_of_plies()
    {
        var pgn = Game(sans: ["e4", "e5", "Nf3", "Nc6"], result: "draw").Pgn;

        Assert.Equal("1. e4 e5 2. Nf3 Nc6 1/2-1/2", MovetextOf(pgn));
    }

    [Fact]
    public void Pgn_has_date_in_dotted_form_and_no_movetext_headers_in_Moves()
    {
        var game = Game(sans: ["e4", "e5", "Nf3"]);

        Assert.Contains("[Date \"2026.10.19\"]", game.Pgn);
        Assert.Equal("e4 e5 Nf3", game.Moves);
    }

    [Fact]
    public void Same_input_gives_same_Id()
    {
        var a = Game();
        var b = Game();

        Assert.Equal(a.Id, b.Id);
        Assert.Matches(new Regex("^otb-[0-9a-f]{12}$"), a.Id);
    }

    [Fact]
    public void Different_moves_give_a_different_Id()
    {
        var a = Game(sans: ["e4", "e5", "Nf3"]);
        var b = Game(sans: ["d4", "d5", "c4"]);

        Assert.NotEqual(a.Id, b.Id);
    }
}
