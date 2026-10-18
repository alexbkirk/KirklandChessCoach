using System.Windows;
using ChessCoach.Core.Abstractions;

namespace ChessCoach.Desktop;

/// <summary>Closes the WPF app, so the installer can replace its files.</summary>
public sealed class WpfAppLifecycle : IAppLifecycle
{
    public void Shutdown() => Application.Current?.Dispatcher.InvokeAsync(Application.Current.Shutdown);
}
