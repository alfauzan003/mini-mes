using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Equipment.Domain;
using MiniMes.Api.Modules.Equipment.Features.Queries;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Results;
using EquipmentEntity = MiniMes.Api.Modules.Equipment.Domain.Equipment;

namespace MiniMes.Api.Modules.Equipment.Features.StatusLog;

public sealed record EquipmentStatusLogDto(
    EquipmentStatus From, EquipmentStatus To, string Reason, DateTimeOffset ChangedAt);

public static class StatusLogEndpoint
{
    private const int DefaultLimit = 100;
    private const int MaxLimit = 500;

    public static void MapStatusLog(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/equipment/{code}/status-log", async (
            string code, int? limit, MesDbContext db, CancellationToken ct) =>
        {
            var normalized = code.Trim().ToUpperInvariant();
            var equipmentId = await db.Set<EquipmentEntity>().AsNoTracking()
                .Where(e => e.Code == normalized)
                .Select(e => (Guid?)e.Id)
                .SingleOrDefaultAsync(ct);
            if (equipmentId is null)
            {
                return ((Result)EquipmentEndpoints.NotFound(code)).ToHttpResult();
            }

            var take = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
            var rows = await db.Set<EquipmentStatusLog>().AsNoTracking()
                .Where(l => l.EquipmentId == equipmentId)
                .OrderByDescending(l => l.ChangedAt).ThenByDescending(l => l.Id)
                .Take(take)
                .Select(l => new EquipmentStatusLogDto(l.From, l.To, l.Reason, l.ChangedAt))
                .ToListAsync(ct);
            return TypedResults.Ok(rows);
        }).RequireAuthorization();
}
