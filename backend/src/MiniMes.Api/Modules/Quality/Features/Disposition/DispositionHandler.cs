using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Carriers.Domain;
using MiniMes.Api.Modules.Identity;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.Lots.Features.Queries;
using MiniMes.Api.Modules.Quality.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.Quality.Features.Dispositions;

public sealed record DispositionRequest(Disposition Decision, string Reason);

/// <summary>
/// Decides what happens to a held lot. Releasing accepts it (a failed lot becomes PASS, and a passing final pancake
/// is finished and counted on its work order); scrapping ends it and frees its carrier. A lot held for a failed
/// inspection also gets the decision written onto that inspection.
/// </summary>
public sealed class DispositionHandler(MesDbContext db, LotQueries queries, ICurrentUser user, TimeProvider time)
{
    public async Task<Result<LotDto>> HandleAsync(string lotId, DispositionRequest request, CancellationToken ct)
    {
        var reason = request.Reason?.Trim();
        if (string.IsNullOrEmpty(reason))
        {
            return new Error(ErrorCodes.ReasonRequired, "A reason is required to disposition a lot.");
        }

        var decided = await db.ExecuteInTransactionAsync<string>(
            token => DecideAsync(lotId, request.Decision, reason, token), ct);
        if (!decided.IsSuccess)
        {
            return decided.Error!;
        }

        return (await queries.GetAsync(decided.Value, ct))!;
    }

    private async Task<Result<string>> DecideAsync(
        string lotId, Disposition decision, string reason, CancellationToken ct)
    {
        var code = lotId.Trim().ToUpperInvariant();
        var lot = await db.Set<Lot>().SingleOrDefaultAsync(l => l.LotId == code, ct);
        if (lot is null)
        {
            return LotEndpoints.LotNotFound(lotId);
        }

        if (lot.Status != LotStatus.Hold)
        {
            return new Error(ErrorCodes.LotNotAvailable, $"Lot {lot.LotId} is {lot.Status} and cannot be dispositioned.");
        }

        var now = time.GetUtcNow();
        if (lot.Quality == QualityStatus.Fail)
        {
            var recorded = await RecordOnInspectionAsync(lot, decision, reason, now, ct);
            if (!recorded.IsSuccess)
            {
                return recorded.Error!;
            }
        }

        var applied = decision == Disposition.Release
            ? await ReleaseAsync(lot, reason, now, ct)
            : await ScrapAsync(lot, reason, now, ct);
        if (!applied.IsSuccess)
        {
            return applied.Error!;
        }

        return lot.LotId;
    }

    /// <summary>A lot with quality FAIL is held for its latest failed inspection, which carries the decision.</summary>
    private async Task<Result> RecordOnInspectionAsync(
        Lot lot, Disposition decision, string reason, DateTimeOffset now, CancellationToken ct)
    {
        var inspection = await db.Set<Inspection>()
            .Where(i => i.LotId == lot.Id && i.Result == InspectionResult.Fail)
            .OrderByDescending(i => i.InspectedAt)
            .FirstOrDefaultAsync(ct);
        return inspection is null
            ? new Error(ErrorCodes.LotNotAvailable, $"Lot {lot.LotId} has no failed inspection to disposition.")
            : inspection.SetDisposition(decision, user.UserId, reason, now);
    }

    private async Task<Result> ReleaseAsync(Lot lot, string reason, DateTimeOffset now, CancellationToken ct)
    {
        var released = lot.Release();
        if (!released.IsSuccess)
        {
            return released;
        }

        db.Set<LotEvent>().Add(LotEvent.Record(lot, LotEventType.Release, user.UserId, now, note: reason));
        if (!lot.IsFinal || lot.Quality != QualityStatus.Pass)
        {
            return Result.Success();
        }

        var finished = lot.Finish();
        if (!finished.IsSuccess)
        {
            return finished;
        }

        db.Set<LotEvent>().Add(LotEvent.Record(lot, LotEventType.Finish, user.UserId, now));
        var workOrder = await db.Set<WorkOrder>().SingleAsync(w => w.Id == lot.WorkOrderId, ct);
        workOrder.RegisterFinishedPancake();
        return Result.Success();
    }

    private async Task<Result> ScrapAsync(Lot lot, string reason, DateTimeOffset now, CancellationToken ct)
    {
        // The unload event is written first because scrapping clears the carrier the event must record.
        if (lot.CurrentCarrierId is { } carrierId)
        {
            db.Set<LotEvent>().Add(LotEvent.Record(lot, LotEventType.CarrierUnload, user.UserId, now));
            var carrier = await db.Set<Carrier>().SingleAsync(c => c.Id == carrierId, ct);
            carrier.Unload();
        }

        var scrapped = lot.Scrap();
        if (!scrapped.IsSuccess)
        {
            return scrapped;
        }

        db.Set<LotEvent>().Add(LotEvent.Record(lot, LotEventType.Scrap, user.UserId, now, note: reason));
        return Result.Success();
    }
}
