using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Identity;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.Lots.Features.Queries;
using MiniMes.Api.Modules.Quality.Domain;
using MiniMes.Api.Shared.Data;

namespace MiniMes.Api.Modules.Quality.Features.Inspections;

public sealed class InspectionQueries(MesDbContext db, LotQueries lots)
{
    public async Task<InspectionDto?> GetAsync(Guid id, CancellationToken ct) =>
        (await ListAsync(db.Set<Inspection>().Where(i => i.Id == id), ct)).SingleOrDefault();

    /// <summary>Inspections of a lot, newest first. Null when the lot does not exist.</summary>
    public async Task<IReadOnlyList<InspectionDto>?> ForLotAsync(string lotId, CancellationToken ct)
    {
        var code = lotId.Trim().ToUpperInvariant();
        var id = await db.Set<Lot>().AsNoTracking()
            .Where(l => l.LotId == code)
            .Select(l => (Guid?)l.Id)
            .SingleOrDefaultAsync(ct);
        if (id is null)
        {
            return null;
        }

        return await ListAsync(db.Set<Inspection>().Where(i => i.LotId == id), ct);
    }

    /// <summary>Lots waiting for a first inspection of their current operation, oldest first.</summary>
    public async Task<IReadOnlyList<LotDto>> QueueAsync(CancellationToken ct)
    {
        var waiting = db.Set<Lot>().AsNoTracking()
            .Where(l => l.Status == LotStatus.Wait && l.Quality == QualityStatus.None)
            .Where(l => db.Set<InspectionSpec>()
                .Any(s => s.ProductId == l.ProductId && s.Operation == l.CurrentOperation));

        // Project orders newest first and the queue is served oldest first, so the cap is applied to the
        // oldest lots by selecting them before projecting.
        var oldest = waiting.OrderBy(l => l.CreatedAt).ThenBy(l => l.LotId).Take(LotQueries.MaxListSize);
        var rows = await lots.Project(oldest).ToListAsync(ct);
        return [.. rows.OrderBy(l => l.CreatedAt).ThenBy(l => l.LotId, StringComparer.Ordinal)];
    }

    private async Task<IReadOnlyList<InspectionDto>> ListAsync(IQueryable<Inspection> inspections, CancellationToken ct)
    {
        var rows = await inspections.AsNoTracking()
            .Include(i => i.Measurements)
            .OrderByDescending(i => i.InspectedAt).ThenBy(i => i.Id)
            .ToListAsync(ct);
        if (rows.Count == 0)
        {
            return [];
        }

        var lotIds = rows.Select(i => i.LotId).Distinct().ToArray();
        var lotCodes = await db.Set<Lot>().AsNoTracking()
            .Where(l => lotIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.LotId, ct);

        var userIds = rows.SelectMany(i => new[] { i.InspectorId, i.DispositionById ?? i.InspectorId }).Distinct().ToArray();
        var usernames = await db.Set<User>().AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Username, ct);

        var defectCodes = rows.Where(i => i.DefectCode is not null).Select(i => i.DefectCode!).Distinct().ToArray();
        var descriptions = await db.Set<DefectCode>().AsNoTracking()
            .Where(d => defectCodes.Contains(d.Code))
            .ToDictionaryAsync(d => d.Code, d => d.Description, ct);

        var specIds = rows.SelectMany(i => i.Measurements).Select(m => m.SpecId).Distinct().ToArray();
        var seqBySpec = await db.Set<InspectionSpec>().AsNoTracking()
            .Where(s => specIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Seq, ct);

        return [.. rows.Select(i => new InspectionDto(
            i.Id,
            lotCodes[i.LotId],
            i.Operation,
            usernames[i.InspectorId],
            i.InspectedAt,
            i.Result,
            i.DefectCode,
            i.DefectCode is null ? null : descriptions.GetValueOrDefault(i.DefectCode),
            i.Reason,
            i.RejectQty,
            i.Disposition,
            i.DispositionById is { } by ? usernames[by] : null,
            i.DispositionAt,
            i.DispositionReason,
            [.. i.Measurements
                .OrderBy(m => seqBySpec[m.SpecId])
                .Select(m => new MeasurementDto(m.ItemName, m.Unit, m.Lsl, m.Usl, m.Value, m.Judgment))]))];
    }
}
