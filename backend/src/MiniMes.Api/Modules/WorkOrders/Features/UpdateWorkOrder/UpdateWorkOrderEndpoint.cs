using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Identity;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Modules.WorkOrders.Features.CreateWorkOrder;
using MiniMes.Api.Modules.WorkOrders.Features.Queries;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Http;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.WorkOrders.Features.UpdateWorkOrder;

public sealed record UpdateWorkOrderRequest(
    int TargetQty,
    DateTimeOffset PlannedStart,
    DateTimeOffset PlannedEnd,
    IReadOnlyList<OperationAssignment> Operations);

public sealed class UpdateWorkOrderHandler(MesDbContext db, WorkOrderQueries queries)
{
    public async Task<Result<WorkOrderDto>> HandleAsync(Guid id, UpdateWorkOrderRequest request, CancellationToken ct)
    {
        var updated = await db.ExecuteInTransactionAsync(async token =>
        {
            var workOrder = await db.Set<WorkOrder>().Include(w => w.Operations)
                .SingleOrDefaultAsync(w => w.Id == id, token);
            if (workOrder is null)
            {
                return WorkOrderEndpoints.NotFound(id);
            }

            var product = await db.Set<Product>().Include(p => p.Route)
                .SingleAsync(p => p.Id == workOrder.ProductId, token);
            var assignments = await AssignmentResolver.ResolveAsync(db, request.Operations, token);
            if (!assignments.IsSuccess)
            {
                return assignments.Error!;
            }

            return workOrder.Update(
                request.TargetQty, request.PlannedStart, request.PlannedEnd, assignments.Value, product);
        }, ct);

        if (!updated.IsSuccess)
        {
            return updated.Error!;
        }

        return (await queries.GetAsync(id, ct))!;
    }
}

public static class UpdateWorkOrderEndpoint
{
    public static void MapUpdateWorkOrder(this IEndpointRouteBuilder app) =>
        app.MapPut("/api/work-orders/{id:guid}", async (
            Guid id, UpdateWorkOrderRequest request, UpdateWorkOrderHandler handler, CancellationToken ct) =>
        {
            if (MalformedBody.HasNullItem(request.Operations))
            {
                return MalformedBody.Problem("Every operation must be an object.");
            }

            return (await handler.HandleAsync(id, request, ct)).ToHttpResult();
        })
            .RequireAuthorization(Policies.Plan);
}
