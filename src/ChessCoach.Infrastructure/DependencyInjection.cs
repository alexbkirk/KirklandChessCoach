using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Settings;
using ChessCoach.Core.Stats;
using ChessCoach.Core.Training;
using ChessCoach.Infrastructure.Data;
using ChessCoach.Infrastructure.Diagnostics;
using ChessCoach.Infrastructure.Secrets;
using ChessCoach.Infrastructure.Setup;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ChessCoach.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddChessCoachInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ChessCoachOptions>(configuration.GetSection(ChessCoachOptions.SectionName));
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<SqliteConnectionFactory>();
        services.AddSingleton<MigrationRunner>();
        services.AddSingleton<IGameRepository, GameRepository>();
        services.AddSingleton<IDiagnosticsService, DiagnosticsService>();
        services.AddSingleton<IPuzzleRepository, PuzzleRepository>();
        services.AddSingleton<PuzzleService>();
        services.AddSingleton<IDrillRepository, DrillRepository>();
        services.AddSingleton<DrillService>();
        services.AddSingleton<IFocusRepository, FocusRepository>();
        services.AddSingleton<WeeklyFocusService>();
        services.AddSingleton<IOpeningRepository, OpeningRepository>();
        services.AddSingleton<OpeningReportService>();
        services.AddSingleton<OpeningVariationService>();
        services.AddSingleton<IStatsRepository, StatsRepository>();
        services.AddSingleton<StatsService>();
        services.AddSingleton<IAppSettingsStore, AppSettingsStore>();
        services.AddSingleton<IEngineInstaller>(sp => new EngineInstaller(new HttpClient { Timeout = TimeSpan.FromMinutes(30) }, sp.GetRequiredService<IOptions<ChessCoachOptions>>()));
        services.AddSingleton<ISetupService, SetupService>();
        services.AddSingleton<IImportRunner, ImportRunner>();
        services.AddSingleton<ThemeSettingsService>();
        services.AddSingleton<IOpponentRepository, OpponentRepository>();
        services.AddSingleton<OpponentService>();
        // Windows only: the desktop host is Windows-only, and the Settings page needs this service.
        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<IApiKeyStore, DpapiApiKeyStore>();
            services.AddSingleton<ApiKeySettingsService>();
        }
        return services;
    }
}
