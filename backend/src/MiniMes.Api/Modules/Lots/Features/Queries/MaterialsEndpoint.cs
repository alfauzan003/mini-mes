using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Data;

namespace MiniMes.Api.Modules.Lots.Features.Queries;

public sealed record MaterialDto(string Code, string Name, LotType Kind, Polarity Polarity, string Uom);

public static class MaterialsEndpoint
{
    public static void MapMaterials(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/materials", async (MesDbContext db, CancellationToken ct) =>
            TypedResults.Ok(await db.Set<Material>().AsNoTracking()
                .OrderBy(m => m.Polarity).ThenBy(m => m.Kind).ThenBy(m => m.Code)
                .Select(m => new MaterialDto(m.Code, m.Name, m.Kind, m.Polarity, m.Uom))
                .ToListAsync(ct)))
            .RequireAuthorization();
}
