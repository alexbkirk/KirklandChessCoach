namespace ChessCoach.Core.Domain;

/// <summary>One game, as the stats tabs need it. Rating and opponent are from the games table; Result is "win", "loss" or "draw".</summary>
public sealed record StatsGame(
    string Id,
    DateOnly Date,
    string Time,
    string Color,
    string Result,
    int MyRating,
    int OppRating,
    string TimeControl,
    string Family,
    string Ending,
    int Plies,
    string Opponent);
