using System.Globalization;
using System.IO;
using System.Windows;
using ChessCoach.Infrastructure;
using ChessCoach.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using MudBlazor.Services;
using Serilog;

namespace ChessCoach.Desktop;

public partial class App : Application
{
    private IHost? _host;

    public IServiceProvider Services => _host?.Services ?? throw new InvalidOperationException("The host has not started yet.");

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            _host = BuildHost(e.Args);
            await _host.StartAsync();
            await _host.Services.GetRequiredService<MigrationRunner>().MigrateAsync();
            new MainWindow().Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ChessCoach could not start.\n\n{ex.Message}", "ChessCoach", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        await Log.CloseAndFlushAsync();
        base.OnExit(e);
    }

    private static IHost BuildHost(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
        });
        // Optional, per machine, and git-ignored (appsettings.*.local.json): e.g. where the analysis scripts live
        // when the app is published outside the repository.
        builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: false);

        var dataDirectory = builder.Configuration[$"{ChessCoachOptions.SectionName}:{nameof(ChessCoachOptions.DataDirectory)}"];
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            dataDirectory = ChessCoachOptions.DefaultDataDirectory;
        }

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(Path.Combine(dataDirectory, "logs", "chesscoach-.log"), formatProvider: CultureInfo.InvariantCulture, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
            .CreateLogger();
        builder.Services.AddSerilog();

        builder.Services.AddSingleton<ChessCoach.Core.Abstractions.IAppLifecycle, WpfAppLifecycle>();
        builder.Services.AddChessCoachInfrastructure(builder.Configuration);
        builder.Services.PostConfigure<ChessCoachOptions>(o =>
        {
            o.AnalysisDirectory ??= FindAnalysisDirectory();
            o.PythonPath ??= ChessCoachOptions.FindPython(AppContext.BaseDirectory, o.AnalysisDirectory);
        });
        builder.Services.AddWpfBlazorWebView();
        builder.Services.AddMudServices();
        builder.Services.AddScoped<ChessCoach.UI.ChessRules>();
        builder.Services.AddScoped<ChessCoach.UI.ThemeState>();
        builder.Services.AddScoped<ChessCoach.UI.AppNavigation>();
        builder.Services.AddScoped<ChessCoach.Core.Settings.BoardView>();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif
        return builder.Build();
    }

    /// <summary>Finds the 'analysis' folder next to the app, or in a parent folder when running from the repo.</summary>
    private static string? FindAnalysisDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "analysis");
            if (File.Exists(Path.Combine(candidate, "refresh.py")))
            {
                return candidate;
            }
        }

        return null;
    }
}



