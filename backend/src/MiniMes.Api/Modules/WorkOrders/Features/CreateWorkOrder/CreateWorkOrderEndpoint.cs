using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Identity;
using MiniMes.Api.Modules.Lots.LotIds;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Modules.WorkOrders.Features.Queries;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Results;
using EquipmentEntity = MiniMes.Api.Modules.Equipment.Domain.Equipment;

namespace MiniMes.Api.Modules.WorkOrders.Features.CreateWorkOrder;

public sealed record OperationAssignment(OperationCode Operation, string EquipmentCode);

public sealed record CreateWorkOrderRequest(
    string ProductCode,
    int TargetQty,
    DateTimeOffset PlannedStart,
    DateTimeOffset PlannedEnd,
    IReadOnlyList<OperationAssignment> Operations);

public sealed class CreateWorkOrderHandler(
    MesDbContext db, LotIdGenerator ids, WorkOrderQueries queries, TimeProvider time)
{
    public async Task<Result<WorkOrderDto>> HandleAsync(CreateWorkOrderRequest request, CancellationToken ct)
    {
        var created = await db.ExecuteInTransactionAsync<Guid>(async token =>
        {
            var productCode = (request.ProductCode ?? "").Trim().ToUpperInvariant();
            var product = await db.Set<Product>().Include(p => p.Route)
                .SingleOrDefaultAsync(p => p.Code == productCode, token);
            if (product is null)
            {
                return new Error(
                    ErrorCodes.ProductNotFound, $"Product '{request.ProductCode}' was not found.", ErrorKind.NotFound);
            }

            var assignments = await AssignmentResolver.ResolveAsync(db, request.Operations, token);
            if (!assignments.IsSuccess)
            {
                return assignments.Error!;
            }

            var number = await ids.NextWorkOrderNumberAsync(token);
            if (!number.IsSuccess)
            {
                return number.Error!;
            }

            var workOrder = WorkOrder.Create(
                number.Value, product, request.TargetQty, request.PlannedStart, request.PlannedEnd,
                assignments.Value, time.GetUtcNow());
            if (!workOrder.IsSuccess)
            {
                return workOrder.Error!;
            }

            db.Set<WorkOrder>().Add(workOrder.Value);
            return workOrder.Value.Id;
        }, ct);

        if (!created.IsSuccess)
        {
            return created.Error!;
        }

        return (await queries.GetAsync(created.Value, ct))!;
    }
}

/// <summary>Turns the equipment codes of a request into equipment entities.</summary>
internal static class AssignmentResolver
{
    public static async Task<Result<List<OperationAssignmentInput>>> ResolveAsync(
        MesDbContext db, IReadOnlyList<OperationAssignment>? operations, CancellationToken ct)
    {
        var requested = operations ?? [];
        var codes = requested.Select(o => Normalize(o.EquipmentCode)).Distinct().ToArray();
        var found = await db.Set<EquipmentEntity>().AsNoTracking()
            .Where(e => codes.Contains(e.Code))
            .ToDictionaryAsync(e => e.Code, ct);

        var inputs = new List<OperationAssignmentInput>();
        foreach (var operation in requested)
        {
            if (!found.TryGetValue(Normalize(operation.EquipmentCode), out var equipment))
            {
                return new Error(
                    ErrorCodes.EquipmentNotFound,
                    $"Equipment '{operation.EquipmentCode}' was not found.",
                    ErrorKind.NotFound);
            }

            inputs.Add(new OperationAssignmentInput(operation.Operation, equipment));
        }

        return inputs;
    }

    private static string Normalize(string? code) => (code ?? "").Trim().ToUpperInvariant();
}

public static class CreateWorkOrderEndpoint
{
    public static void MapCreateWorkOrder(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/work-orders", async (
            CreateWorkOrderRequest request, CreateWorkOrderHandler handler, CancellationToken ct) =>
            (await handler.HandleAsync(request, ct))
                .ToHttpResult(wo => TypedResults.Created($"/api/work-orders/{wo.Id}", wo)))
            .RequireAuthorization(Policies.Plan);
}
