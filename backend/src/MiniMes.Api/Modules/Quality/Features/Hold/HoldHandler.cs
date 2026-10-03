using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Identity;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.Lots.Features.Queries;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.Quality.Features.Hold;

public sealed record HoldLotRequest(string Reason);

/// <summary>Puts a waiting lot on hold by hand, with the reason recorded in the lot history.</summary>
public sealed class HoldHandler(MesDbContext db, LotQueries queries, ICurrentUser user, TimeProvider time)
{
    public async Task<Result<LotDto>> HandleAsync(string lotId, HoldLotRequest request, CancellationToken ct)
    {
        var reason = request.Reason?.Trim();
        if (string.IsNullOrEmpty(reason))
        {
            return new Error(ErrorCodes.ReasonRequired, "A reason is required to hold a lot.");
        }

        var tooLong = ReasonRules.CheckLength(reason);
        if (tooLong is not null)
        {
            return tooLong;
        }

        var held = await db.ExecuteInTransactionAsync<string>(token => HoldAsync(lotId, reason, token), ct);
        if (!held.IsSuccess)
        {
            return held.Error!;
        }

        return (await queries.GetAsync(held.Value, ct))!;
    }

    private async Task<Result<string>> HoldAsync(string lotId, string reason, CancellationToken ct)
    {
        var code = lotId.Trim().ToUpperInvariant();
        var lot = await db.Set<Lot>().SingleOrDefaultAsync(l => l.LotId == code, ct);
        if (lot is null)
        {
            return LotEndpoints.LotNotFound(lotId);
        }

        var held = lot.Hold();
        if (!held.IsSuccess)
        {
            return held.Error!;
        }

        db.Set<LotEvent>().Add(LotEvent.Record(lot, LotEventType.Hold, user.UserId, time.GetUtcNow(), note: reason));
        return lot.LotId;
    }
}
