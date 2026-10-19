using System.Windows;

namespace ChessCoach.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        BlazorWebView.Services = ((App)Application.Current).Services;
        BlazorWebView.BlazorWebViewInitialized += (_, e) =>
            e.WebView.CoreWebView2.DocumentTitleChanged += (sender, _) => Title = TitleFor(((Microsoft.Web.WebView2.Core.CoreWebView2)sender!).DocumentTitle);
    }

    /// <summary>The page names itself "Stats · ChessCoach"; the window shows that, or just the app name before any page has loaded.</summary>
    private static string TitleFor(string? documentTitle) => string.IsNullOrWhiteSpace(documentTitle) ? "ChessCoach" : documentTitle;
}
