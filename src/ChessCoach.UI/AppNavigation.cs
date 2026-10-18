using ChessCoach.Core.Navigation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace ChessCoach.UI;

/// <summary>
/// Remembers the pages the user has been through, for the Back button, and lets a page keep its tab and filter in its own address so
/// that going back returns to the same tab. One per window.
/// </summary>
public sealed class AppNavigation : IDisposable
{
    private readonly NavigationManager _navigation;
    private readonly PageHistory _history = new();

    public AppNavigation(NavigationManager navigation)
    {
        _navigation = navigation;
        _history.Arrive(navigation.Uri);
        _navigation.LocationChanged += OnLocationChanged;
    }

    /// <summary>Raised when the Back button may have turned on or off.</summary>
    public event Action? Changed;

    public bool CanGoBack => _history.CanGoBack;

    public void GoBack()
    {
        var previous = _history.Back();
        if (previous is null) return;

        _navigation.NavigateTo(previous);
        Changed?.Invoke();
    }

    /// <summary>
    /// Changes the query of the page being shown (for example its tab) without adding a step to Back, so Back still leaves the page.
    /// A value of null removes that parameter.
    /// </summary>
    public void ReplaceQuery(IReadOnlyDictionary<string, object?> parameters)
    {
        var target = _navigation.GetUriWithQueryParameters(parameters);
        if (target == _navigation.Uri) return;

        _history.ReplaceNext();
        _navigation.NavigateTo(target, replace: true);
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        _history.Arrive(e.Location);
        Changed?.Invoke();
    }

    public void Dispose() => _navigation.LocationChanged -= OnLocationChanged;
}
