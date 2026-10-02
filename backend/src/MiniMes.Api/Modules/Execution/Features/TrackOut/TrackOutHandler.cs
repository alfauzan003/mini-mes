using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Carriers.Domain;
using MiniMes.Api.Modules.Execution.Domain;
using MiniMes.Api.Modules.Execution.Features.Queries;
using MiniMes.Api.Modules.Identity;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Results;
using EquipmentEntity = MiniMes.Api.Modules.Equipment.Domain.Equipment;

namespace MiniMes.Api.Modules.Execution.Features.TrackOut;

/// <summary>
/// Ends a run: each input lot still running is charged its consumed quantity (the rest returns to WAIT),
/// emptied carriers are unloaded, and the equipment is freed.
/// </summary>
public sealed class TrackOutHandler(
    MesDbContext db,
    RunQueries queries,
    ICurrentUser user,
    TimeProvider time)
{
    /// <summary>Quantities are stored as numeric(12,3); more precision would be silently rounded.</summary>
    private const int QtyDecimals = 3;

    public async Task<Result<RunDto>> HandleAsync(Guid runId, TrackOutRequest request, CancellationToken ct)
    {
        var consumptions = request.Consumptions ?? [];
        var ended = await db.ExecuteInTransactionAsync<Guid>(token => EndRunAsync(runId, consumptions, token), ct);
        if (!ended.IsSuccess)
        {
            return ended.Error!;
        }

        return (await queries.GetAsync(runId, ct))!;
    }

    private async Task<Result<Guid>> EndRunAsync(
        Guid runId, IReadOnlyList<Consumption> consumptions, CancellationToken ct)
    {
        // Lock first, load after: a double-submitted track-out must see the first one's ended run.
        if (!await db.AcquireAsync(runId, ct))
        {
            return new Error(ErrorCodes.RunNotFound, $"Run '{runId}' was not found.", ErrorKind.NotFound);
        }

        var run = await db.Set<ProductionRun>().Include(r => r.Inputs).Include(r => r.Outputs)
            .SingleAsync(r => r.Id == runId, ct);
        if (!run.IsOpen)
        {
            return new Error(ErrorCodes.RunNotOpen, "The production run is already closed.");
        }

        var inputIds = run.Inputs.Select(i => i.LotId).ToArray();
        var lots = await db.Set<Lot>().Where(l => inputIds.Contains(l.Id)).OrderBy(l => l.LotId).ToListAsync(ct);

        var listed = ResolveConsumptions(consumptions, lots);
        if (!listed.IsSuccess)
        {
            return listed.Error!;
        }

        // An input that already left RUN (the calendered roll went back to WAIT at produce) is not consumed.
        var plan = lots
            .Where(l => l.Status == LotStatus.Run)
            .Select(l => (Lot: l, Qty: listed.Value.GetValueOrDefault(l.Id, l.Qty)))
            .ToList();
        var tooMuch = plan.FirstOrDefault(p => p.Qty > p.Lot.Qty).Lot;
        if (tooMuch is not null)
        {
            return new Error(ErrorCodes.QtyExceedsLot, $"Lot {tooMuch.LotId} has only {tooMuch.Qty} {tooMuch.Uom}.");
        }

        var step = await db.Set<WorkOrderOperation>().SingleAsync(o => o.Id == run.WorkOrderOperationId, ct);
        var equipment = await db.Set<EquipmentEntity>().SingleAsync(e => e.Id == run.EquipmentId, ct);
        var carrierIds = plan
            .Where(p => p.Qty == p.Lot.Qty && p.Lot.CurrentCarrierId is not null)
            .Select(p => p.Lot.CurrentCarrierId!.Value)
            .ToArray();
        var carriers = await db.Set<Carrier>().Where(c => carrierIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);

        var now = time.GetUtcNow();
        foreach (var (lot, qty) in plan)
        {
            // Events are written before the lot changes: using it up clears its carrier, which they must record.
            db.Set<LotEvent>().Add(LotEvent.Record(
                lot, LotEventType.TrackOut, user.UserId, now, runId: run.Id, qty: qty, operation: step.Operation));
            if (qty == lot.Qty && lot.CurrentCarrierId is { } carrierId)
            {
                db.Set<LotEvent>().Add(LotEvent.Record(lot, LotEventType.CarrierUnload, user.UserId, now, run.Id));
                carriers[carrierId].Unload();
            }

            var consumed = lot.Consume(qty);
            if (!consumed.IsSuccess)
            {
                return consumed.Error!;
            }
        }

        var ended = run.End(plan.ToDictionary(p => p.Lot.Id, p => p.Qty), now);
        if (!ended.IsSuccess)
        {
            return ended.Error!;
        }

        equipment.EndRun();
        return run.Id;
    }

    /// <summary>Maps each listed lot to its consumed quantity, rejecting anything that is not a clean input.</summary>
    private static Result<Dictionary<Guid, decimal>> ResolveConsumptions(
        IReadOnlyList<Consumption> consumptions, IReadOnlyList<Lot> inputs)
    {
        var byCode = inputs.ToDictionary(l => l.LotId);
        var listed = new Dictionary<Guid, decimal>();
        foreach (var item in consumptions)
        {
            var code = (item.LotId ?? "").Trim().ToUpperInvariant();
            if (!byCode.TryGetValue(code, out var lot))
            {
                return new Error(ErrorCodes.InvalidInputSet, $"Lot '{item.LotId}' is not an input of this run.");
            }

            if (listed.ContainsKey(lot.Id))
            {
                return new Error(ErrorCodes.InvalidInputSet, $"Lot {lot.LotId} is listed more than once.");
            }

            if (item.ConsumedQty < 0 || decimal.Round(item.ConsumedQty, QtyDecimals) != item.ConsumedQty)
            {
                return new Error(
                    ErrorCodes.InvalidQuantity,
                    $"Consumed quantity of {lot.LotId} cannot be negative and has at most {QtyDecimals} decimals.");
            }

            listed[lot.Id] = item.ConsumedQty;
        }

        return listed;
    }
}
