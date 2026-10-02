using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Carriers.Domain;
using MiniMes.Api.Modules.Execution.Domain;
using MiniMes.Api.Modules.Identity;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Data;
using EquipmentEntity = MiniMes.Api.Modules.Equipment.Domain.Equipment;

namespace MiniMes.Api.Modules.Execution.Features.Queries;

public sealed class RunQueries(MesDbContext db)
{
    public const int MaxListSize = 200;

    public async Task<RunDto?> GetAsync(Guid id, CancellationToken ct) =>
        (await LoadAsync(db.Set<ProductionRun>().AsNoTracking().Where(r => r.Id == id), ct)).SingleOrDefault();

    /// <summary>Newest first. <paramref name="open"/> null means open and closed runs.</summary>
    public async Task<IReadOnlyList<RunDto>> ListAsync(string? equipmentCode, bool? open, CancellationToken ct)
    {
        var runs = db.Set<ProductionRun>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(equipmentCode))
        {
            var code = equipmentCode.Trim().ToUpperInvariant();
            runs = runs.Where(r => db.Set<EquipmentEntity>().Any(e => e.Id == r.EquipmentId && e.Code == code));
        }

        if (open is { } wantOpen)
        {
            runs = wantOpen ? runs.Where(r => r.EndedAt == null) : runs.Where(r => r.EndedAt != null);
        }

        return await LoadAsync(runs.OrderByDescending(r => r.StartedAt).Take(MaxListSize), ct);
    }

    /// <summary>The given runs by ID; IDs that match no run are left out.</summary>
    public async Task<IReadOnlyDictionary<Guid, RunDto>> GetManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, RunDto>();
        }

        var wanted = ids.ToArray();
        var runs = await LoadAsync(db.Set<ProductionRun>().AsNoTracking().Where(r => wanted.Contains(r.Id)), ct);
        return runs.ToDictionary(r => r.Id);
    }

    private async Task<List<RunDto>> LoadAsync(IQueryable<ProductionRun> runs, CancellationToken ct)
    {
        var headers = await (
            from r in runs
            join e in db.Set<EquipmentEntity>() on r.EquipmentId equals e.Id
            join o in db.Set<WorkOrderOperation>() on r.WorkOrderOperationId equals o.Id
            join w in db.Set<WorkOrder>() on o.WorkOrderId equals w.Id
            join u in db.Set<User>() on r.OperatorId equals u.Id
            select new
            {
                r.Id,
                EquipmentCode = e.Code,
                WorkOrderNumber = w.Number,
                r.WorkOrderOperationId,
                o.Operation,
                Operator = u.Username,
                r.StartedAt,
                r.EndedAt,
                r.GoodQty,
                r.RejectQty
            }).ToListAsync(ct);
        if (headers.Count == 0)
        {
            return [];
        }

        var ids = headers.Select(h => h.Id).ToArray();
        var inputs = await (
            from i in db.Set<RunInput>().AsNoTracking()
            where ids.Contains(i.RunId)
            join l in db.Set<Lot>() on i.LotId equals l.Id
            orderby i.Role, l.LotId
            select new { i.RunId, Dto = new RunInputDto(l.LotId, l.Type, i.Role, l.Qty, l.Uom, i.ConsumedQty) })
            .ToListAsync(ct);
        var outputs = await (
            from o in db.Set<RunOutput>().AsNoTracking()
            where ids.Contains(o.RunId)
            join l in db.Set<Lot>() on o.LotId equals (Guid?)l.Id into lots
            from l in lots.DefaultIfEmpty()
            join c in db.Set<Carrier>() on o.CarrierId equals (Guid?)c.Id into carriers
            from c in carriers.DefaultIfEmpty()
            orderby o.Lane, l.LotId
            select new { o.RunId, Dto = new RunOutputDto(l.LotId, c.Code, o.Lane, o.GoodQty, o.RejectQty) })
            .ToListAsync(ct);

        var inputsByRun = inputs.ToLookup(i => i.RunId, i => i.Dto);
        var outputsByRun = outputs.ToLookup(o => o.RunId, o => o.Dto);
        return headers
            .OrderByDescending(h => h.StartedAt)
            .Select(h =>
            {
                var runInputs = inputsByRun[h.Id].ToList();
                return new RunDto(
                    h.Id, h.EquipmentCode, h.WorkOrderNumber, h.WorkOrderOperationId, h.Operation, h.Operator,
                    h.StartedAt, h.EndedAt, h.GoodQty, h.RejectQty,
                    runInputs.FirstOrDefault(i => i.Role == RunInputRole.Primary)?.LotId,
                    runInputs, outputsByRun[h.Id].ToList());
            }).ToList();
    }
}
