using MiniMes.Api.Modules.Identity;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.Execution.Features.TrackIn;

public static class TrackInEndpoint
{
    public static void MapTrackIn(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/runs/track-in", async (
            TrackInRequest request, TrackInHandler handler, CancellationToken ct) =>
            (await handler.HandleAsync(request, ct))
                .ToHttpResult(run => TypedResults.Created($"/api/runs/{run.Id}", run)))
            .RequireAuthorization(Policies.Operate);
}
