namespace ChessCoach.UI.Components;

/// <summary>A move the user made on the board: UCI for the grader, SAN for display.</summary>
public sealed record BoardMove(string Uci, string San);

/// <summary>A circle on <paramref name="Square"/>, or an arrow to <paramref name="To"/>. Brush: green, red, blue or yellow.</summary>
public sealed record BoardMark(string Square, string Brush, string? To = null);
