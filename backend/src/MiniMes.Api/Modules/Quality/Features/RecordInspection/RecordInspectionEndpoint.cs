using MiniMes.Api.Modules.Identity;
using MiniMes.Api.Shared.Http;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.Quality.Features.RecordInspection;

public static class RecordInspectionEndpoint
{
    public static void MapRecordInspection(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/lots/{lotId}/inspections", async (
            string lotId, RecordInspectionRequest request, RecordInspectionHandler handler, CancellationToken ct) =>
        {
            if (request.Measurements is null || MalformedBody.HasNullItem(request.Measurements))
            {
                return MalformedBody.Problem("Give a list of measurements, each with a specId and a value.");
            }

            return (await handler.HandleAsync(lotId, request, ct))
                .ToHttpResult(inspection => TypedResults.Created($"/api/lots/{inspection.LotId}/inspections", inspection));
        })
            .RequireAuthorization(Policies.Inspect);
}
