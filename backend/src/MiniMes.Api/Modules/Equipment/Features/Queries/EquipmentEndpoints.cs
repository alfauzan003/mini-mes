using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Equipment.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Http;
using MiniMes.Api.Shared.Results;
using EquipmentEntity = MiniMes.Api.Modules.Equipment.Domain.Equipment;

namespace MiniMes.Api.Modules.Equipment.Features.Queries;

public sealed record EquipmentDto(
    string Code, string Name, OperationCode Operation, int? LaneCount, EquipmentStatus Status);

public static class EquipmentEndpoints
{
    public static void MapEquipmentQueries(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/equipment", async (string? operation, MesDbContext db, CancellationToken ct) =>
        {
            if (!EnumQuery.TryParse<OperationCode>(operation, out var op))
            {
                return EnumQuery.Invalid(nameof(operation), operation);
            }

            var query = db.Set<EquipmentEntity>().AsNoTracking();
            if (op is { } filter)
            {
                query = query.Where(e => e.Operation == filter);
            }

            var items = await query
                .OrderBy(e => e.Code)
                .Select(e => new EquipmentDto(e.Code, e.Name, e.Operation, e.LaneCount, e.Status))
                .ToListAsync(ct);
            return TypedResults.Ok(items);
        }).RequireAuthorization();

        app.MapGet("/api/equipment/{code}", async (string code, MesDbContext db, CancellationToken ct) =>
        {
            var normalized = code.Trim().ToUpperInvariant();
            var dto = await db.Set<EquipmentEntity>().AsNoTracking()
                .Where(e => e.Code == normalized)
                .Select(e => new EquipmentDto(e.Code, e.Name, e.Operation, e.LaneCount, e.Status))
                .SingleOrDefaultAsync(ct);

            Result<EquipmentDto> result = dto is not null
                ? dto
                : new Error(ErrorCodes.EquipmentNotFound, $"Equipment '{code}' was not found.", ErrorKind.NotFound);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}
