using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Carriers.Domain;
using MiniMes.Api.Modules.Execution.Domain;
using MiniMes.Api.Modules.Execution.Features.Queries;
using MiniMes.Api.Modules.Identity;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Quantities;
using MiniMes.Api.Shared.Results;
using EquipmentEntity = MiniMes.Api.Modules.Equipment.Domain.Equipment;

namespace MiniMes.Api.Modules.Execution.Features.TrackOut;

/// <summary>
/// Ends a run: each input lot still running on the run's equipment is charged its consumed quantity (the rest
/// returns to WAIT), emptied carriers are unloaded, and the equipment is freed. Calendering never consumes its
/// roll: a roll it did not get to calender simply goes back to WAIT unchanged. Slitting uses up its electrode, so
/// it cannot be tracked out before its output is recorded.
/// </summary>
public sealed class TrackOutHandler(
    MesDbContext db,
    RunQueries queries,
    ICurrentUser user,
    TimeProvider time)
{
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

        var step = await db.Set<WorkOrderOperation>().SingleAsync(o => o.Id == run.WorkOrderOperationId, ct);
        if (step.Operation == OperationCode.Slit && run.Outputs.Count == 0)
        {
            // Slitting always uses up the electrode, so ending the run with no pancakes would lose it for good.
            return new Error(
                ErrorCodes.InvalidOutputSet, "Slitting needs its output recorded before track out.");
        }

        var inputIds = run.Inputs.Select(i => i.LotId).ToArray();
        var lots = await db.Set<Lot>().Where(l => inputIds.Contains(l.Id)).OrderBy(l => l.LotId).ToListAsync(ct);

        var listed = ResolveConsumptions(consumptions, lots, step.Operation, run.PrimaryLotId);
        if (!listed.IsSuccess)
        {
            return listed.Error!;
        }

        // A lot is this run's to consume only while it is RUN on the run's equipment: the calendered roll goes back
        // to WAIT at produce and may already be running on the slitter by the time calendering is tracked out.
        var running = lots.Where(l => l.Status == LotStatus.Run && l.CurrentEquipmentId == run.EquipmentId).ToList();
        var unusedRoll = step.Operation == OperationCode.Cal ? running.SingleOrDefault(l => l.Id == run.PrimaryLotId) : null;
        var plan = running
            .Where(l => l != unusedRoll)
            .Select(l => (Lot: l, Qty: listed.Value.GetValueOrDefault(l.Id, l.Qty)))
            .ToList();
        var tooMuch = plan.FirstOrDefault(p => p.Qty > p.Lot.Qty).Lot;
        if (tooMuch is not null)
        {
            return new Error(ErrorCodes.QtyExceedsLot, $"Lot {tooMuch.LotId} has only {tooMuch.Qty} {tooMuch.Uom}.");
        }

        var equipment = await db.Set<EquipmentEntity>().SingleAsync(e => e.Id == run.EquipmentId, ct);
        var carrierIds = plan
            .Where(p => p.Qty == p.Lot.Qty && p.Lot.CurrentCarrierId is not null)
            .Select(p => p.Lot.CurrentCarrierId!.Value)
            .ToArray();
        var carriers = await db.Set<Carrier>().Where(c => carrierIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);

        var now = time.GetUtcNow();
        var consumedByLot = plan.ToDictionary(p => p.Lot.Id, p => p.Qty);
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

        if (unusedRoll is not null)
        {
            db.Set<LotEvent>().Add(LotEvent.Record(
                unusedRoll, LotEventType.TrackOut, user.UserId, now, runId: run.Id, qty: 0m, operation: step.Operation));
            var returned = unusedRoll.ReturnToWait();
            if (!returned.IsSuccess)
            {
                return returned.Error!;
            }

            consumedByLot[unusedRoll.Id] = 0m;
        }

        var ended = run.End(consumedByLot, now);
        if (!ended.IsSuccess)
        {
            return ended.Error!;
        }

        equipment.EndRun();
        return run.Id;
    }

    /// <summary>Maps each listed lot to its consumed quantity, rejecting anything that is not a clean input.</summary>
    private static Result<Dictionary<Guid, decimal>> ResolveConsumptions(
        IReadOnlyList<Consumption> consumptions, IReadOnlyList<Lot> inputs, OperationCode operation, Guid? primaryLotId)
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

            var isPrimary = lot.Id == primaryLotId;
            if (operation == OperationCode.Cal && isPrimary)
            {
                return new Error(
                    ErrorCodes.InvalidInputSet, $"Calendering never consumes its roll, so {lot.LotId} cannot be listed.");
            }

            var qty = item.ConsumedQty ?? 0m;
            if (qty < 0)
            {
                return new Error(ErrorCodes.InvalidQuantity, $"Consumed quantity of {lot.LotId} cannot be negative.");
            }

            if (QuantityRules.CheckFits(qty, $"Consumed quantity of {lot.LotId}") is { } badQty)
            {
                return badQty;
            }

            if (operation == OperationCode.Slit && isPrimary && qty != lot.Qty)
            {
                return new Error(
                    ErrorCodes.InvalidQuantity,
                    $"Slitting uses up the whole electrode: {lot.LotId} must be consumed in full ({lot.Qty} {lot.Uom}).");
            }

            listed[lot.Id] = qty;
        }

        return listed;
    }
}
