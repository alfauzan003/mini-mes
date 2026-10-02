using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Carriers.Domain;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Http;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.Carriers.Features.Queries;

/// <param name="LotId">The string ID of the lot loaded on the carrier; null when empty.</param>
public sealed record CarrierDto(string Code, string Type, CarrierStatus Status, string? LotId);

public static class CarrierEndpoints
{
    public static void MapCarrierQueries(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/carriers", async (string? type, string? status, MesDbContext db, CancellationToken ct) =>
        {
            if (!EnumQuery.TryParse<CarrierStatus>(status, out var carrierStatus))
            {
                return EnumQuery.Invalid(nameof(status), status);
            }

            var query = db.Set<Carrier>().AsNoTracking();
            if (!string.IsNullOrWhiteSpace(type))
            {
                var typeCode = type.Trim().ToUpperInvariant();
                query = query.Where(c => c.TypeCode == typeCode);
            }

            if (carrierStatus is { } filter)
            {
                query = query.Where(c => c.Status == filter);
            }

            var items = await Project(query.OrderBy(c => c.Code), db).ToListAsync(ct);
            return TypedResults.Ok(items);
        }).RequireAuthorization();

        app.MapGet("/api/carriers/{code}", async (string code, MesDbContext db, CancellationToken ct) =>
        {
            var normalized = code.Trim().ToUpperInvariant();
            var dto = await Project(db.Set<Carrier>().AsNoTracking().Where(c => c.Code == normalized), db)
                .SingleOrDefaultAsync(ct);

            Result<CarrierDto> result = dto is not null
                ? dto
                : new Error(ErrorCodes.CarrierNotFound, $"Carrier '{code}' was not found.", ErrorKind.NotFound);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }

    private static IQueryable<CarrierDto> Project(IQueryable<Carrier> carriers, MesDbContext db) =>
        carriers.Select(c => new CarrierDto(
            c.Code, c.TypeCode, c.Status,
            db.Set<Lot>().Where(l => l.Id == c.CurrentLotId).Select(l => l.LotId).FirstOrDefault()));
}
