using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Results;
using MiniMes.Api.Shared.Time;

namespace MiniMes.Api.Modules.Lots.LotIds;

/// <summary>
/// Issues gap-free daily sequence numbers with an atomic upsert. The caller must already be inside a
/// transaction so a rolled-back command also releases its number.
/// </summary>
public sealed class LotIdGenerator(MesDbContext db, PlantCalendar calendar, TimeProvider time)
{
    public async Task<Result<string>> NextLotIdAsync(
        LotType type, Polarity polarity, string? equipmentCode, CancellationToken ct)
    {
        var prefix = LotIdFormat.Prefix(type, polarity, PlantToday(), equipmentCode);
        return LotIdFormat.Compose(prefix, type, await NextValueAsync(prefix, ct));
    }

    public async Task<Result<string>> NextWorkOrderNumberAsync(CancellationToken ct)
    {
        var prefix = LotIdFormat.WorkOrderPrefix(PlantToday());
        return LotIdFormat.ComposeWorkOrder(prefix, await NextValueAsync(prefix, ct));
    }

    private DateOnly PlantToday() => calendar.DateOf(time.GetUtcNow());

    private async Task<int> NextValueAsync(string prefix, CancellationToken ct)
    {
        // INSERT ... RETURNING is not composable, so materialize with ToListAsync instead of a LINQ terminal.
        var values = await db.Database
            .SqlQuery<int>($"""
                INSERT INTO lot.id_sequence (prefix, last_value) VALUES ({prefix}, 1)
                ON CONFLICT (prefix) DO UPDATE SET last_value = lot.id_sequence.last_value + 1
                RETURNING last_value AS "Value"
                """)
            .ToListAsync(ct);
        return values.Single();
    }
}
