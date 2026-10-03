using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Equipment.Features.Queries;
using MiniMes.Api.Modules.Equipment.Machine;
using MiniMes.Api.Modules.Identity;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Results;
using EquipmentEntity = MiniMes.Api.Modules.Equipment.Domain.Equipment;

namespace MiniMes.Api.Modules.Equipment.Features.InjectFault;

public static class InjectFaultEndpoint
{
    public const string InjectFaultMethod = "InjectFault";

    public static void MapInjectFault(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/equipment/{code}/inject-fault", async (
            string code, MesDbContext db, SimulatorPresence presence, IHubContext<MachineHub> machines,
            CancellationToken ct) =>
        {
            var normalized = code.Trim().ToUpperInvariant();
            if (!await db.Set<EquipmentEntity>().AnyAsync(e => e.Code == normalized, ct))
            {
                return ((Result)EquipmentEndpoints.NotFound(code)).ToHttpResult();
            }

            if (presence.Count == 0)
            {
                return ((Result)new Error(
                    ErrorCodes.SimulatorOffline, "No equipment simulator is connected.", ErrorKind.Conflict))
                    .ToHttpResult();
            }

            await machines.Clients.Group(MachineHub.SimulatorsGroup).SendAsync(InjectFaultMethod, normalized, ct);
            return TypedResults.Accepted((string?)null);
        }).RequireAuthorization(Policies.Admin);
}
