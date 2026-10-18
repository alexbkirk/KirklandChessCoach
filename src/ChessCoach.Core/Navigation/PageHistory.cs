namespace ChessCoach.Core.Navigation;

/// <summary>
/// The pages the user has been through, newest last, so a Back button knows where to return to and when there is nowhere to go.
/// Kept apart from the browser's own history so it can be tested and so Back can be disabled at the start.
/// </summary>
public sealed class PageHistory(int capacity = PageHistory.DefaultCapacity)
{
    public const int DefaultCapacity = 50;

    private readonly List<string> _entries = [];
    private bool _replaceNext;

    /// <summary>The page being shown, or null before the first one.</summary>
    public string? Current => _entries.Count == 0 ? null : _entries[^1];

    public bool CanGoBack => _entries.Count > 1;

    public int Count => _entries.Count;

    /// <summary>
    /// Marks the next arrival as a change to the page already shown (such as picking a tab), which updates its entry
    /// instead of adding one. Used once: the arrival after it is an ordinary visit again.
    /// </summary>
    public void ReplaceNext() => _replaceNext = true;

    /// <summary>
    /// The user arrived at <paramref name="location"/>. A new page is added; the same page again changes nothing; a change
    /// marked with <see cref="ReplaceNext"/> replaces the current entry; and arriving at the page just before the current one
    /// (the browser's own Back, or Alt+Left) counts as going back, so the two never get out of step.
    /// </summary>
    public void Arrive(string location)
    {
        var replace = _replaceNext;
        _replaceNext = false;

        if (_entries.Count == 0)
        {
            _entries.Add(location);
            return;
        }

        if (location == Current) return;

        if (replace)
        {
            _entries[^1] = location;
            return;
        }

        if (_entries.Count > 1 && location == _entries[^2])
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
        _replaceNext = false;
        return Current;
    }
}
