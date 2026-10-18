namespace ChessCoach.Core.Navigation;

/// <summary>
/// The pages the user has been through, newest last, so a Back button knows where to return to and when there is nowhere to go.
/// Kept apart from the browser's own history so it can be tested and so Back can be disabled at the start.
/// </summary>
public sealed class PageHistory(int capacity = PageHistory.DefaultCapacity)
{
    public const int DefaultCapacity = 50;

    private readonly List<string> _entries = [];
    private string? _replaceTarget;

    /// <summary>The page being shown, or null before the first one.</summary>
    public string? Current => _entries.Count == 0 ? null : _entries[^1];

    public bool CanGoBack => _entries.Count > 1;

    public int Count => _entries.Count;

    /// <summary>
    /// Says that the next arrival, if it is at <paramref name="location"/>, is a change to the page already shown (such as picking a
    /// tab), so it updates that entry instead of adding one. It holds for one arrival only: any other arrival cancels it, so a change
    /// that never happens cannot swallow a later visit.
    /// </summary>
    public void ExpectReplace(string location) => _replaceTarget = location;

    /// <summary>
    /// The user arrived at <paramref name="location"/>. A new page is added; the same page again changes nothing; a change announced
    /// with <see cref="ExpectReplace"/> replaces the current entry; and arriving at the page just before the current one counts as
    /// going back, so the browser's own Back (or Alt+Left) and this history never get out of step.
    /// </summary>
    /// <param name="mayBeBack">
    /// False when the arrival is known not to be the browser's own Back, such as a click on a link. Then reaching the previous page
    /// again is a new visit (A, then B, then A by a link keeps all three), not a step back.
    /// </param>
    public void Arrive(string location, bool mayBeBack = true)
    {
        var replaceTarget = _replaceTarget;
        _replaceTarget = null;

        if (_entries.Count == 0)
        {
            _entries.Add(location);
            return;
        }

        if (location == Current) return;

        if (replaceTarget == location)
        {
            _entries[^1] = location;
            return;
        }

        if (mayBeBack && _entries.Count > 1 && location == _entries[^2])
        {
            _entries.RemoveAt(_entries.Count - 1);
            return;
        }

        _entries.Add(location);
        if (_entries.Count > capacity) _entries.RemoveAt(0);
    }

    /// <summary>Goes back one page and returns it, or null when there is no earlier page.</summary>
    public string? Back()
    {
        if (!CanGoBack) return null;
        _entries.RemoveAt(_entries.Count - 1);
        _replaceTarget = null;
        return Current;
    }
}
