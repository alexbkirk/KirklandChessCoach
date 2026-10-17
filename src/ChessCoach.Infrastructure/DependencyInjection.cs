using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Training;
using ChessCoach.Infrastructure.Data;
using ChessCoach.Infrastructure.Diagnostics;
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
        return services;
    }
}
