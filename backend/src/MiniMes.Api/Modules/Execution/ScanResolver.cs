using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Carriers.Domain;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.Execution;

/// <summary>Turns a scanned barcode (a lot ID, or a carrier code that holds a lot) into the tracked lot.</summary>
public sealed class ScanResolver(MesDbContext db)
{
    public async Task<Result<Lot>> ResolveAsync(string? scan, CancellationToken ct)
    {
        var code = (scan ?? "").Trim().ToUpperInvariant();
        if (code.Length > 0)
        {
            var lot = await db.Set<Lot>().SingleOrDefaultAsync(l => l.LotId == code, ct);
            if (lot is not null)
            {
                return lot;
            }

            var loaded = await (
                from c in db.Set<Carrier>()
                where c.Code == code && c.CurrentLotId != null
                join l in db.Set<Lot>() on c.CurrentLotId equals (Guid?)l.Id
                select l).SingleOrDefaultAsync(ct);
            if (loaded is not null)
            {
                return loaded;
            }
        }

        return new Error(
            ErrorCodes.ScanNotResolved,
            $"'{scan}' matches no lot and no loaded carrier.",
            ErrorKind.NotFound);
    }
}
