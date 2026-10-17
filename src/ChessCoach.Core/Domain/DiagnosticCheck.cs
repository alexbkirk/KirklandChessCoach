namespace ChessCoach.Core.Domain;

public enum DiagnosticStatus
{
    Ok,
    Warning,
    Error,
}

public sealed record DiagnosticCheck(string Name, DiagnosticStatus Status, string Detail);
