namespace ChessCoach.UI;

/// <summary>Links to the variation page (Pages/OpeningVariation.razor).</summary>
public static class OpeningLinks
{
    /// <summary>The page for one opening variation (exact name) as <paramref name="color"/>; the family is passed along for a link back to it.</summary>
    public static string Variation(string color, string family, string opening) =>
        $"{Family(color, family)}&opening={Uri.EscapeDataString(opening)}";

    /// <summary>The page for a whole opening family as <paramref name="color"/>.</summary>
    public static string Family(string color, string family) =>
        $"openings?color={Uri.EscapeDataString(color)}&family={Uri.EscapeDataString(family)}";
}
