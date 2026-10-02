using MiniMes.Api.Modules.Lots.Features.Queries;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.Quality.Features.Inspections;

public static class InspectionEndpoints
{
    public static void MapInspectionQueries(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/lots/{lotId}/inspections", async (string lotId, InspectionQueries queries, CancellationToken ct) =>
        {
            var inspections = await queries.ForLotAsync(lotId, ct);
            // Result<T> cannot wrap an interface type, so the not-found case goes through the non-generic Result.
            return inspections is not null
                ? TypedResults.Ok(inspections)
                : ((Result)LotEndpoints.LotNotFound(lotId)).ToHttpResult();
        }).RequireAuthorization();
}
