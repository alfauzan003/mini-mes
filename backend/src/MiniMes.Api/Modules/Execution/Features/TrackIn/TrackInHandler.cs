using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Execution.Domain;
using MiniMes.Api.Modules.Execution.Features.Queries;
using MiniMes.Api.Modules.Identity;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Quality;
using MiniMes.Api.Shared.Results;
using EquipmentEntity = MiniMes.Api.Modules.Equipment.Domain.Equipment;

namespace MiniMes.Api.Modules.Execution.Features.TrackIn;

public sealed class TrackInHandler(
    MesDbContext db,
    ScanResolver scans,
    IInspectionRequirement inspection,
    RunQueries queries,
    ICurrentUser user,
    TimeProvider time)
{
    public async Task<Result<RunDto>> HandleAsync(TrackInRequest request, CancellationToken ct)
    {
        var started = await db.ExecuteInTransactionAsync<Guid>(token => StartRunAsync(request, token), ct);
        if (!started.IsSuccess)
        {
            return started.Error!;
        }

        return (await queries.GetAsync(started.Value, ct))!;
    }

    private async Task<Result<Guid>> StartRunAsync(TrackInRequest request, CancellationToken ct)
    {
        var equipmentCode = (request.EquipmentCode ?? "").Trim().ToUpperInvariant();
        var equipment = await db.Set<EquipmentEntity>().SingleOrDefaultAsync(e => e.Code == equipmentCode, ct);
        if (equipment is null)
        {
            return new Error(
                ErrorCodes.EquipmentNotFound, $"Equipment '{request.EquipmentCode}' was not found.", ErrorKind.NotFound);
        }

        var workOrder = await db.Set<WorkOrder>().Include(w => w.Operations)
            .SingleOrDefaultAsync(w => w.Operations.Any(o => o.Id == request.WorkOrderOperationId), ct);
        if (workOrder is null)
        {
            return new Error(
                ErrorCodes.WoNotFound,
                $"Work order operation '{request.WorkOrderOperationId}' was not found.",
                ErrorKind.NotFound);
        }

        var step = workOrder.Operations.Single(o => o.Id == request.WorkOrderOperationId);
        if (step.EquipmentId != equipment.Id)
        {
            return new Error(
                ErrorCodes.EquipmentNotAssigned,
                $"Equipment {equipment.Code} is not assigned to {step.Operation} of {workOrder.Number}.");
        }

        var active = workOrder.EnsureCanTrackIn();
        if (!active.IsSuccess)
        {
            return active.Error!;
        }

        var available = equipment.EnsureCanStartRun();
        if (!available.IsSuccess)
        {
            return available.Error!;
        }

        var lots = new List<Lot>();
        foreach (var scan in request.Inputs ?? [])
        {
            var lot = await scans.ResolveAsync(scan, ct);
            if (!lot.IsSuccess)
            {
                return lot.Error!;
            }

            lots.Add(lot.Value);
        }

        var inputs = OperationInputRules.Classify(step.Operation, lots);
        if (!inputs.IsSuccess)
        {
            return inputs.Error!;
        }

        var product = await db.Set<Product>().SingleAsync(p => p.Id == workOrder.ProductId, ct);
        foreach (var (lot, _) in inputs.Value)
        {
            var error = await CheckLotAsync(lot, step.Operation, workOrder, product, ct);
            if (error is not null)
            {
                return error;
            }
        }

        var now = time.GetUtcNow();
        var run = ProductionRun.Start(
            step.Id, equipment.Id, user.UserId, inputs.Value.Select(i => (i.Lot.Id, i.Role)), now);
        foreach (var (lot, _) in inputs.Value)
        {
            var tracked = lot.TrackIn(equipment.Id);
            if (!tracked.IsSuccess)
            {
                return tracked.Error!;
            }

            db.Set<LotEvent>().Add(
                LotEvent.Record(
                    lot, LotEventType.TrackIn, user.UserId, now, runId: run.Id, qty: lot.Qty,
                    operation: step.Operation));
        }

        var runStarted = equipment.StartRun(run.Id);
        if (!runStarted.IsSuccess)
        {
            return runStarted.Error!;
        }

        workOrder.OnTrackIn();
        db.Set<ProductionRun>().Add(run);
        return run.Id;
    }

    private async Task<Error?> CheckLotAsync(
        Lot lot, OperationCode operation, WorkOrder workOrder, Product product, CancellationToken ct)
    {
        if (lot.Status != LotStatus.Wait)
        {
            return new Error(ErrorCodes.LotNotAvailable, $"Lot {lot.LotId} is {lot.Status} and cannot be tracked in.");
        }

        if (lot.Polarity != product.Polarity)
        {
            return new Error(
                ErrorCodes.PolarityMismatch,
                $"Lot {lot.LotId} is {lot.Polarity}, but {workOrder.Number} makes {product.Polarity} product.");
        }

        if (lot.Type is not (LotType.Raw or LotType.Foil) && lot.WorkOrderId != workOrder.Id)
        {
            return new Error(ErrorCodes.LotWoMismatch, $"Lot {lot.LotId} belongs to a different work order.");
        }

        if (lot.NextOperation != operation)
        {
            return new Error(
                ErrorCodes.RouteViolation,
                $"Lot {lot.LotId} is not due for {operation}; its next operation is {lot.NextOperation?.ToString() ?? "none"}.");
        }

        if (lot.Quality != QualityStatus.Pass
            && lot.CurrentOperation is { } produced
            && await inspection.IsRequiredAsync(product.Id, produced, ct))
        {
            return new Error(
                ErrorCodes.LotQualityPending, $"Lot {lot.LotId} has not passed inspection after {produced}.");
        }

        return null;
    }
}
