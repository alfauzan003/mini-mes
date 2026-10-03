using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Equipment.Features.Queries;
using MiniMes.Api.Modules.Execution.Features.Queries;
using MiniMes.Api.Modules.Identity;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Results;
using EquipmentEntity = MiniMes.Api.Modules.Equipment.Domain.Equipment;

namespace MiniMes.Api.Modules.Equipment.Features.Maintenance;

public sealed class MaintenanceHandler(MesDbContext db, RunQueries runs)
{
    public Task<Result<EquipmentDto>> StartAsync(string code, CancellationToken ct) =>
        ChangeAsync(code, e => e.StartMaintenance(), ct);

    public Task<Result<EquipmentDto>> EndAsync(string code, CancellationToken ct) =>
        ChangeAsync(code, e => e.EndMaintenance(), ct);

    private async Task<Result<EquipmentDto>> ChangeAsync(
        string code, Func<EquipmentEntity, Result> change, CancellationToken ct)
    {
        var normalized = code.Trim().ToUpperInvariant();
        var changed = await db.ExecuteInTransactionAsync(async token =>
        {
            var equipment = await db.Set<EquipmentEntity>().SingleOrDefaultAsync(e => e.Code == normalized, token);
            if (equipment is null)
            {
                return EquipmentEndpoints.NotFound(code);
            }

            return change(equipment);
        }, ct);
        if (!changed.IsSuccess)
        {
            return changed.Error!;
        }

        return (await EquipmentEndpoints.FindAsync(normalized, db, runs, ct))!;
    }
}

public static class MaintenanceEndpoints
{
    public static void MapMaintenance(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/equipment/{code}/maintenance/start", async (
            string code, MaintenanceHandler handler, CancellationToken ct) =>
            (await handler.StartAsync(code, ct)).ToHttpResult())
            .RequireAuthorization(Policies.Admin);

        app.MapPost("/api/equipment/{code}/maintenance/end", async (
            string code, MaintenanceHandler handler, CancellationToken ct) =>
            (await handler.EndAsync(code, ct)).ToHttpResult())
            .RequireAuthorization(Policies.Admin);
    }
}
