namespace ChessCoach.Core.Domain;

/// <summary>Headline numbers for the games database. Dates are ISO strings (yyyy-MM-dd) as stored.</summary>
public sealed record DatabaseSummary(int GameCount, int MoveCount, string? FirstGameDate, string? LastGameDate);
