using System.Globalization;

namespace ChessCoach.Core.Navigation;

/// <summary>Reads the tab and colour a page keeps in its address, ignoring anything that is not a real choice.</summary>
public static class QueryChoice
{
    /// <summary>
    /// The tab to show: the number in the address when it is a whole number from 0 up to the last tab, otherwise the first tab.
    /// A sign, spaces, text or a number that is too big all fall back to the first tab.
    /// </summary>
    public static int Tab(string? raw, int tabCount) =>
        int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var tab) && tab < tabCount ? tab : 0;

    /// <summary>"white" or "black" as written in the address; anything else means both colours (null).</summary>
    public static string? Color(string? raw) => raw is "white" or "black" ? raw : null;

    /// <summary>
    /// How many rows a "Show more" list shows: the whole number in the address, rounded down to a whole page and kept between one page and
    /// <paramref name="max"/>; anything else means one page.
    /// </summary>
    public static int Count(string? raw, int page, int max) =>
        int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var count) && count >= page ? Math.Min(count, max) / page * page : page;
}
