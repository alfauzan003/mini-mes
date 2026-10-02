using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Carriers.Domain;
using MiniMes.Api.Modules.Identity;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Data;
using EquipmentEntity = MiniMes.Api.Modules.Equipment.Domain.Equipment;

namespace MiniMes.Api.Modules.Lots.Features.Queries;

/// <param name="Statuses">Empty means any status.</param>
/// <param name="WorkOrder">Exact work order number.</param>
/// <param name="Material">Exact material code.</param>
/// <param name="Search">Case-insensitive prefix of the lot ID or of the current carrier code.</param>
public sealed record LotFilter(
    IReadOnlyList<LotStatus> Statuses,
    LotType? Type = null,
    OperationCode? NextOperation = null,
    string? WorkOrder = null,
    string? Material = null,
    string? Search = null);

public sealed class LotQueries(MesDbContext db)
{
    public const int MaxListSize = 200;

    public async Task<LotDto?> GetAsync(string lotId, CancellationToken ct)
    {
        var normalized = Normalize(lotId);
        return await Project(db.Set<Lot>().AsNoTracking().Where(l => l.LotId == normalized))
            .SingleOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<LotDto>> ListAsync(LotFilter filter, CancellationToken ct)
    {
        var lots = db.Set<Lot>().AsNoTracking();

        if (filter.Statuses.Count > 0)
        {
            var statuses = filter.Statuses.ToArray();
            lots = lots.Where(l => statuses.Contains(l.Status));
        }

        if (filter.Type is { } type)
        {
            lots = lots.Where(l => l.Type == type);
        }

        if (filter.NextOperation is { } next)
        {
            lots = lots.Where(l => l.NextOperation == next);
        }

        if (!string.IsNullOrWhiteSpace(filter.WorkOrder))
        {
            var number = Normalize(filter.WorkOrder);
            lots = lots.Where(l => db.Set<WorkOrder>().Any(w => w.Id == l.WorkOrderId && w.Number == number));
        }

        if (!string.IsNullOrWhiteSpace(filter.Material))
        {
            var code = Normalize(filter.Material);
            lots = lots.Where(l => db.Set<Material>().Any(m => m.Id == l.MaterialId && m.Code == code));
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var prefix = Normalize(filter.Search);
            lots = lots.Where(l =>
                l.LotId.ToUpper().StartsWith(prefix)
                || db.Set<Carrier>().Any(c => c.Id == l.CurrentCarrierId && c.Code.ToUpper().StartsWith(prefix)));
        }

        return await Project(lots).Take(MaxListSize).ToListAsync(ct);
    }

    /// <summary>History of a lot, oldest first. Null when the lot does not exist.</summary>
    public async Task<IReadOnlyList<LotEventDto>?> EventsAsync(string lotId, CancellationToken ct)
    {
        var normalized = Normalize(lotId);
        var id = await db.Set<Lot>().AsNoTracking()
            .Where(l => l.LotId == normalized)
            .Select(l => (Guid?)l.Id)
            .SingleOrDefaultAsync(ct);
        if (id is null)
        {
            return null;
        }

        return await (
            from e in db.Set<LotEvent>().AsNoTracking()
            where e.LotId == id
            join u in db.Set<User>() on e.UserId equals u.Id
            join q in db.Set<EquipmentEntity>() on e.EquipmentId equals (Guid?)q.Id into equipment
            from q in equipment.DefaultIfEmpty()
            join c in db.Set<Carrier>() on e.CarrierId equals (Guid?)c.Id into carriers
            from c in carriers.DefaultIfEmpty()
            orderby e.OccurredAt, e.Id
            select new LotEventDto(
                e.Id, e.Type, e.Operation, q.Code, c.Code, e.RunId, u.Username, e.Qty, e.Note, e.OccurredAt))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Adds the codes behind the lot's foreign keys, newest lot first. Translates to SQL, so callers can keep
    /// composing on it (Skip/Take, Where on the source lots), but ordering has to happen here because EF cannot
    /// order by a member of the constructed DTO.
    /// </summary>
    public IQueryable<LotDto> Project(IQueryable<Lot> lots) =>
        from l in lots
        join p in db.Set<Product>() on l.ProductId equals (Guid?)p.Id into products
        from p in products.DefaultIfEmpty()
        join m in db.Set<Material>() on l.MaterialId equals (Guid?)m.Id into materials
        from m in materials.DefaultIfEmpty()
        join w in db.Set<WorkOrder>() on l.WorkOrderId equals (Guid?)w.Id into workOrders
        from w in workOrders.DefaultIfEmpty()
        join q in db.Set<EquipmentEntity>() on l.CurrentEquipmentId equals (Guid?)q.Id into equipment
        from q in equipment.DefaultIfEmpty()
        join c in db.Set<Carrier>() on l.CurrentCarrierId equals (Guid?)c.Id into carriers
        from c in carriers.DefaultIfEmpty()
        orderby l.CreatedAt descending, l.LotId descending
        select new LotDto(
            l.LotId, l.Type, l.Polarity, p.Code, m.Code, w.Number, l.Qty, l.Uom, l.Status, l.Quality,
            l.CurrentOperation, l.NextOperation, q.Code, c.Code, l.CreatedAt);

    private static string Normalize(string code) => code.Trim().ToUpperInvariant();
}
