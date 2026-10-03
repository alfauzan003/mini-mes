using MiniMes.Api.Modules.Equipment.Features.Assignments;
using MiniMes.Api.Modules.Equipment.Features.InjectFault;
using MiniMes.Api.Modules.Equipment.Features.Maintenance;
using MiniMes.Api.Modules.Equipment.Machine;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MiniMes.Api.Modules.Equipment.Features.Queries;
using MiniMes.Api.Modules.Equipment.Features.Readings;
using MiniMes.Api.Modules.Equipment.Parameters;
using MiniMes.Api.Modules.Equipment.Features.StatusLog;

namespace MiniMes.Api.Modules.Equipment;

public static class EquipmentModule
{
    public static IServiceCollection AddEquipmentModule(this IServiceCollection services)
    {
        services.AddScoped<MaintenanceHandler>();
        services.AddOptions<ParametersOptions>().BindConfiguration(ParametersOptions.Section);
        services.AddSingleton<LatestReadings>();
        services.AddSingleton<SimulatorPresence>();
        services.AddScoped<ReadingRecorder>();
        services.AddScoped<ReadingRetention>();
        services.AddHostedService<ReadingRetentionService>();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }

    public static IEndpointRouteBuilder MapEquipmentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapEquipmentQueries();
        app.MapAssignments();
        app.MapMaintenance();
        app.MapStatusLog();
        app.MapReadings();
        app.MapInjectFault();
        return app;
    }
}
