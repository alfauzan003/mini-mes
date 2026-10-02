using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Quality.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Data;
using MiniMes.Api.Shared.Http;

namespace MiniMes.Api.Modules.Quality.Features.DefectCodes;

/// <param name="Operation">The only operation the code applies to; null for general codes.</param>
public sealed record DefectCodeDto(string Code, string Description, OperationCode? Operation);

public static class DefectCodeEndpoints
{
    public static void MapDefectCodeEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/defect-codes", async (string? operation, MesDbContext db, CancellationToken ct) =>
        {
            if (!EnumQuery.TryParse<OperationCode>(operation, out var op))
            {
                return EnumQuery.Invalid(nameof(operation), operation);
            }

            var query = db.Set<DefectCode>().AsNoTracking();
            if (op is { } filter)
            {
                query = query.Where(d => d.Operation == null || d.Operation == filter);
            }

            var rows = await query.OrderBy(d => d.Code).ToListAsync(ct);
            return TypedResults.Ok(rows.Select(d => new DefectCodeDto(d.Code, d.Description, d.Operation)).ToList());
        }).RequireAuthorization();
}
