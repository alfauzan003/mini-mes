using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Identity;
using MiniMes.Api.Modules.Quality.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Http;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.Quality.Features.Specs;

public sealed record UpdateSpecLimitsRequest(decimal Lsl, decimal Usl);

public sealed class UpdateSpecLimitsHandler(MesDbContext db)
{
    // Inspection limits and measured values are stored as numeric(12,4).
    private const decimal MaxLimit = 99_999_999.9999m;
    private const int LimitDecimals = 4;

    public async Task<Result<InspectionSpecDto>> HandleAsync(Guid id, UpdateSpecLimitsRequest request, CancellationToken ct)
    {
        var spec = await db.Set<InspectionSpec>().SingleOrDefaultAsync(s => s.Id == id, ct);
        if (spec is null)
        {
            return new Error(ErrorCodes.SpecNotFound, $"Inspection spec '{id}' was not found.", ErrorKind.NotFound);
        }

        if (!FitsColumn(request.Lsl) || !FitsColumn(request.Usl))
        {
            return new Error(
                ErrorCodes.InvalidSpecLimits,
                $"Spec limits must lie within ±{MaxLimit} and have at most {LimitDecimals} decimals.");
        }

        var updated = spec.UpdateLimits(request.Lsl, request.Usl);
        if (!updated.IsSuccess)
        {
            return updated.Error!;
        }

        await db.SaveChangesAsync(ct);
        var productCode = await db.Set<Product>().Where(p => p.Id == spec.ProductId).Select(p => p.Code).SingleAsync(ct);
        return SpecEndpoints.ToDto(spec, productCode);
    }

    private static bool FitsColumn(decimal value) =>
        Math.Abs(value) <= MaxLimit && decimal.Round(value, LimitDecimals) == value;
}

public static class SpecEndpoints
{
    public static void MapSpecEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/specs", async (string? product, string? operation, MesDbContext db, CancellationToken ct) =>
        {
            if (!EnumQuery.TryParse<OperationCode>(operation, out var op))
            {
                return EnumQuery.Invalid(nameof(operation), operation);
            }

            var query = from s in db.Set<InspectionSpec>().AsNoTracking()
                        join p in db.Set<Product>() on s.ProductId equals p.Id
                        select new { Spec = s, ProductCode = p.Code };
            if (!string.IsNullOrWhiteSpace(product))
            {
                var code = product.Trim().ToUpperInvariant();
                query = query.Where(x => x.ProductCode == code);
            }

            if (op is { } filter)
            {
                query = query.Where(x => x.Spec.Operation == filter);
            }

            // Operations sort by route order (the enum), not alphabetically as their stored names would.
            var rows = await query.ToListAsync(ct);
            var items = rows
                .OrderBy(x => x.ProductCode, StringComparer.Ordinal)
                .ThenBy(x => x.Spec.Operation)
                .ThenBy(x => x.Spec.Seq)
                .Select(x => ToDto(x.Spec, x.ProductCode))
                .ToList();
            return TypedResults.Ok(items);
        }).RequireAuthorization();

        app.MapPut("/api/specs/{id:guid}", async (
            Guid id, UpdateSpecLimitsRequest request, UpdateSpecLimitsHandler handler, CancellationToken ct) =>
            (await handler.HandleAsync(id, request, ct)).ToHttpResult())
            .RequireAuthorization(Policies.Inspect);
    }

    internal static InspectionSpecDto ToDto(InspectionSpec spec, string productCode) =>
        new(spec.Id, productCode, spec.Operation, spec.ItemName, spec.Unit, spec.Lsl, spec.Usl, spec.Seq);
}
