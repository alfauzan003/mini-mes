using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Identity;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.Lots.Features.Queries;
using MiniMes.Api.Modules.Quality.Domain;
using MiniMes.Api.Modules.Quality.Features.Inspections;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.Quality.Features.RecordInspection;

public sealed record MeasurementInput(Guid SpecId, decimal Value);

public sealed record RecordInspectionRequest(
    IReadOnlyList<MeasurementInput> Measurements, string? DefectCode, string? Reason, decimal? RejectQty);

/// <summary>
/// Judges a lot against the spec of its current operation and applies the outcome: a pass keeps it waiting with
/// quality PASS (a passing final pancake is finished and counted on its work order), a fail holds it. Two QC users
/// racing on one lot are settled by the lot's row version: the loser gets a conflict or LOT_NOT_AVAILABLE.
/// </summary>
public sealed class RecordInspectionHandler(
    MesDbContext db,
    InspectionQueries queries,
    ICurrentUser user,
    TimeProvider time)
{
    public async Task<Result<InspectionDto>> HandleAsync(string lotId, RecordInspectionRequest request, CancellationToken ct)
    {
        var recorded = await db.ExecuteInTransactionAsync<Guid>(token => RecordAsync(lotId, request, token), ct);
        if (!recorded.IsSuccess)
        {
            return recorded.Error!;
        }

        return (await queries.GetAsync(recorded.Value, ct))!;
    }

    private async Task<Result<Guid>> RecordAsync(string lotId, RecordInspectionRequest request, CancellationToken ct)
    {
        var code = lotId.Trim().ToUpperInvariant();
        var lot = await db.Set<Lot>().SingleOrDefaultAsync(l => l.LotId == code, ct);
        if (lot is null)
        {
            return LotEndpoints.LotNotFound(lotId);
        }

        // Material lots have no product, so they have no spec either; saying so beats "not available".
        var specs = lot is { ProductId: { } productId, CurrentOperation: { } operation }
            ? await db.Set<InspectionSpec>()
                .Where(s => s.ProductId == productId && s.Operation == operation)
                .OrderBy(s => s.Seq)
                .ToListAsync(ct)
            : [];
        if (specs.Count == 0)
        {
            return new Error(ErrorCodes.NoInspectionSpec, $"No inspection spec is defined for lot {lot.LotId}.");
        }

        if (!lot.CanBeInspected)
        {
            return new Error(
                ErrorCodes.LotNotAvailable,
                $"Lot {lot.LotId} is {lot.Status} with quality {lot.Quality} and cannot be inspected.");
        }

        DefectCode? defect = null;
        if (!string.IsNullOrWhiteSpace(request.DefectCode))
        {
            var defectCode = request.DefectCode.Trim().ToUpperInvariant();
            defect = await db.Set<DefectCode>().SingleOrDefaultAsync(d => d.Code == defectCode, ct);
            if (defect is null)
            {
                return new Error(ErrorCodes.DefectCodeNotFound, $"Defect code '{request.DefectCode}' was not found.");
            }
        }

        var now = time.GetUtcNow();
        var values = request.Measurements.Select(m => (m.SpecId, m.Value)).ToList();
        var recorded = Inspection.Record(lot, specs, values, defect, request.Reason, request.RejectQty, user.UserId, now);
        if (!recorded.IsSuccess)
        {
            return recorded.Error!;
        }

        var inspection = recorded.Value;
        var applied = lot.ApplyInspection(inspection.Result);
        if (!applied.IsSuccess)
        {
            return applied.Error!;
        }

        db.Set<Inspection>().Add(inspection);
        var passed = inspection.Result == InspectionResult.Pass;
        db.Set<LotEvent>().Add(LotEvent.Record(
            lot, LotEventType.Inspect, user.UserId, now,
            note: passed ? "PASS" : $"FAIL {inspection.DefectCode}: {inspection.Reason}"));

        if (!passed)
        {
            db.Set<LotEvent>().Add(LotEvent.Record(lot, LotEventType.Hold, user.UserId, now, note: inspection.Reason));
        }
        else if (lot.IsFinal)
        {
            var finished = await FinishAsync(lot, now, ct);
            if (!finished.IsSuccess)
            {
                return finished.Error!;
            }
        }

        return inspection.Id;
    }

    private async Task<Result> FinishAsync(Lot lot, DateTimeOffset now, CancellationToken ct)
    {
        var finished = lot.Finish();
        if (!finished.IsSuccess)
        {
            return finished.Error!;
        }

        db.Set<LotEvent>().Add(LotEvent.Record(lot, LotEventType.Finish, user.UserId, now));
        var workOrder = await db.Set<WorkOrder>().SingleAsync(w => w.Id == lot.WorkOrderId, ct);
        workOrder.RegisterFinishedPancake();
        return Result.Success();
    }
}
