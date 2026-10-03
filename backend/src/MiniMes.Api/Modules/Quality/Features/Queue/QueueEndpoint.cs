using MiniMes.Api.Modules.Quality.Features.Inspections;

namespace MiniMes.Api.Modules.Quality.Features.Queue;

public static class QueueEndpoint
{
    public static void MapInspectionQueue(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/inspections/queue", async (InspectionQueries queries, CancellationToken ct) =>
            TypedResults.Ok(await queries.QueueAsync(ct))).RequireAuthorization();
}
