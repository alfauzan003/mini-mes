using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Data;

namespace MiniMes.Api.Modules.WorkOrders.Features.Products;

public sealed record RouteStepDto(OperationCode Operation, string Name, int Seq, string Uom);

public sealed record ProductDto(string Code, string Name, Polarity Polarity, IReadOnlyList<RouteStepDto> Route);

public static class ProductsEndpoint
{
    public static void MapProducts(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/products", async (MesDbContext db, CancellationToken ct) =>
        {
            var operations = await db.Set<Operation>().AsNoTracking().ToDictionaryAsync(o => o.Code, ct);
            var products = await db.Set<Product>().AsNoTracking()
                .Include(p => p.Route)
                .OrderBy(p => p.Code)
                .ToListAsync(ct);

            return TypedResults.Ok(products.Select(p => new ProductDto(
                p.Code,
                p.Name,
                p.Polarity,
                p.Route.Select(s => new RouteStepDto(
                    s.Operation, operations[s.Operation].Name, s.Seq, operations[s.Operation].Uom)).ToList())));
        }).RequireAuthorization();
}
