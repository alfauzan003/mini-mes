using MiniMes.Api.Modules.Equipment.Features.Assignments;
using MiniMes.Api.Modules.Equipment.Features.Queries;

namespace MiniMes.Api.Modules.Equipment;

public static class EquipmentModule
{
    public static IEndpointRouteBuilder MapEquipmentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapEquipmentQueries();
        app.MapAssignments();
        return app;
    }
}
