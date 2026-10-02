using MiniMes.Api.Modules.Lots.Features.Queries;
using MiniMes.Api.Shared.Http;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.Lots.Features.Genealogy;

public static class GenealogyEndpoint
{
    public static void MapGenealogy(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/lots/{lotId}/genealogy", async (
            string lotId, string? direction, GenealogyQuery query, CancellationToken ct) =>
        {
            if (!EnumQuery.TryParse<GenealogyDirection>(direction, out var parsed))
            {
                return EnumQuery.Invalid(nameof(direction), direction);
            }

            var graph = await query.GetAsync(lotId, parsed ?? GenealogyDirection.Backward, ct);
            Result<GenealogyGraph> result = graph is not null ? graph : LotEndpoints.LotNotFound(lotId);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}
