namespace ChessCoach.UI.Components;

/// <summary>A move the user made on the board: UCI for the grader, SAN for display.</summary>
public sealed record BoardMove(string Uci, string San);
