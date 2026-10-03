using MiniMes.Api.Modules.Equipment.Features.Assignments;
using MiniMes.Api.Modules.Equipment.Features.Maintenance;
using MiniMes.Api.Modules.Equipment.Features.Queries;
using MiniMes.Api.Modules.Equipment.Features.StatusLog;

namespace MiniMes.Api.Modules.Equipment;

public static class EquipmentModule
{
    public static IServiceCollection AddEquipmentModule(this IServiceCollection services)
    {
        services.AddScoped<MaintenanceHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapEquipmentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapEquipmentQueries();
        app.MapAssignments();
        app.MapMaintenance();
        app.MapStatusLog();
        return app;
    }
}
