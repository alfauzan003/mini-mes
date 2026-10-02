using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Execution.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Data;
using EquipmentEntity = MiniMes.Api.Modules.Equipment.Domain.Equipment;

namespace MiniMes.Api.Modules.WorkOrders.Features.Queries;

public sealed class WorkOrderQueries(MesDbContext db)
{
    public const int MaxListSize = 200;

    public async Task<WorkOrderDto?> GetAsync(Guid id, CancellationToken ct) =>
        (await LoadAsync(db.Set<WorkOrder>().AsNoTracking().Where(w => w.Id == id), ct)).SingleOrDefault();

    /// <summary>Newest first. An empty <paramref name="statuses"/> means any status.</summary>
    public async Task<IReadOnlyList<WorkOrderDto>> ListAsync(
        IReadOnlyList<WorkOrderStatus> statuses, CancellationToken ct)
    {
        var orders = db.Set<WorkOrder>().AsNoTracking();
        if (statuses.Count > 0)
        {
            var wanted = statuses.ToArray();
            orders = orders.Where(w => wanted.Contains(w.Status));
        }

        return await LoadAsync(
            orders.OrderByDescending(w => w.CreatedAt).ThenByDescending(w => w.Number).Take(MaxListSize), ct);
    }

    private async Task<List<WorkOrderDto>> LoadAsync(IQueryable<WorkOrder> orders, CancellationToken ct)
    {
        var headers = await (
            from w in orders
            join p in db.Set<Product>() on w.ProductId equals p.Id
            select new { Order = w, ProductCode = p.Code, ProductName = p.Name, p.Polarity })
            .ToListAsync(ct);
        if (headers.Count == 0)
        {
            return [];
        }

        var ids = headers.Select(h => h.Order.Id).ToArray();
        var runs = db.Set<ProductionRun>().AsNoTracking();
        var operations = await (
            from o in db.Set<WorkOrderOperation>().AsNoTracking()
            where ids.Contains(o.WorkOrderId)
            join e in db.Set<EquipmentEntity>() on o.EquipmentId equals e.Id
            orderby o.Seq
            select new
            {
                o.WorkOrderId,
                Dto = new WorkOrderOperationDto(
                    o.Id, o.Operation, o.Seq, e.Code,
                    runs.Count(r => r.WorkOrderOperationId == o.Id),
                    runs.Where(r => r.WorkOrderOperationId == o.Id && r.EndedAt != null)
                        .Sum(r => (decimal?)r.GoodQty) ?? 0m)
            })
            .ToListAsync(ct);
        var byOrder = operations.ToLookup(o => o.WorkOrderId, o => o.Dto);

        return headers.Select(h => new WorkOrderDto(
            h.Order.Id, h.Order.Number, h.ProductCode, h.ProductName, h.Polarity, h.Order.TargetQty,
            h.Order.GoodCount, h.Order.Status, h.Order.PlannedStart, h.Order.PlannedEnd,
            byOrder[h.Order.Id].ToList())).ToList();
    }
}
