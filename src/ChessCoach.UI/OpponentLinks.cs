namespace ChessCoach.UI;

/// <summary>Links to the opponent page (Pages/Opponent.razor).</summary>
public static class OpponentLinks
{
    public static string For(string name) => $"opponent?name={Uri.EscapeDataString(name)}";
}
