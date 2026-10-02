using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Identity;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Modules.WorkOrders.Features.Queries;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.WorkOrders.Features.ChangeStatus;

public sealed class ChangeStatusHandler(MesDbContext db, WorkOrderQueries queries)
{
    public async Task<Result<WorkOrderDto>> HandleAsync(
        Guid id, Func<WorkOrder, Result> transition, CancellationToken ct)
    {
        var changed = await db.ExecuteInTransactionAsync(async token =>
        {
            var workOrder = await db.Set<WorkOrder>().SingleOrDefaultAsync(w => w.Id == id, token);
            return workOrder is null ? WorkOrderEndpoints.NotFound(id) : transition(workOrder);
        }, ct);

        if (!changed.IsSuccess)
        {
            return changed.Error!;
        }

        return (await queries.GetAsync(id, ct))!;
    }
}

public static class ChangeStatusEndpoints
{
    public static void MapChangeStatus(this IEndpointRouteBuilder app)
    {
        MapTransition(app, "release", w => w.Release());
        MapTransition(app, "hold", w => w.Hold());
        MapTransition(app, "resume", w => w.Resume());
        MapTransition(app, "complete", w => w.Complete());
    }

    private static void MapTransition(IEndpointRouteBuilder app, string action, Func<WorkOrder, Result> transition) =>
        app.MapPost($"/api/work-orders/{{id:guid}}/{action}", async (
            Guid id, ChangeStatusHandler handler, CancellationToken ct) =>
            (await handler.HandleAsync(id, transition, ct)).ToHttpResult())
            .RequireAuthorization(Policies.Plan);
}
