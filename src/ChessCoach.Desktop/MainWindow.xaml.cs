using System.Windows;

namespace ChessCoach.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        BlazorWebView.Services = ((App)Application.Current).Services;
    }
}
