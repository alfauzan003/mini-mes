using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Results;
using EquipmentEntity = MiniMes.Api.Modules.Equipment.Domain.Equipment;

namespace MiniMes.Api.Modules.Equipment.Features.Assignments;

public sealed record AssignmentDto(
    Guid WorkOrderOperationId,
    string WorkOrderNumber,
    string ProductCode,
    WorkOrderStatus Status,
    int TargetQty,
    int GoodCount);

public static class AssignmentsEndpoint
{
    private static readonly WorkOrderStatus[] ActiveStatuses =
        [WorkOrderStatus.Released, WorkOrderStatus.Running, WorkOrderStatus.Hold];

    public static void MapAssignments(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/equipment/{code}/assignments", async (string code, MesDbContext db, CancellationToken ct) =>
        {
            var normalized = code.Trim().ToUpperInvariant();
            var equipmentId = await db.Set<EquipmentEntity>().AsNoTracking()
                .Where(e => e.Code == normalized)
                .Select(e => (Guid?)e.Id)
                .SingleOrDefaultAsync(ct);
            if (equipmentId is null)
            {
                return ((Result)new Error(
                    ErrorCodes.EquipmentNotFound, $"Equipment '{code}' was not found.", ErrorKind.NotFound))
                    .ToHttpResult();
            }

            var assignments = await (
                from o in db.Set<WorkOrderOperation>().AsNoTracking()
                where o.EquipmentId == equipmentId
                join w in db.Set<WorkOrder>() on o.WorkOrderId equals w.Id
                where ActiveStatuses.Contains(w.Status)
                join p in db.Set<Product>() on w.ProductId equals p.Id
                orderby w.PlannedStart, w.Number
                select new AssignmentDto(o.Id, w.Number, p.Code, w.Status, w.TargetQty, w.GoodCount))
                .ToListAsync(ct);
            return TypedResults.Ok(assignments);
        }).RequireAuthorization();
}
