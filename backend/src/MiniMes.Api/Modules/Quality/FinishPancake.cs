using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.Quality;

/// <summary>A final pancake that passed quality is finished and counted on its work order.</summary>
internal static class FinishPancake
{
    public static async Task<Result> ApplyAsync(
        MesDbContext db, Lot lot, Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        var finished = lot.Finish();
        if (!finished.IsSuccess)
        {
            return finished;
        }

        db.Set<LotEvent>().Add(LotEvent.Record(lot, LotEventType.Finish, userId, now));
        var workOrder = await db.Set<WorkOrder>().SingleAsync(w => w.Id == lot.WorkOrderId, ct);
        workOrder.RegisterFinishedPancake();
        return Result.Success();
    }
}
