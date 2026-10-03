using MiniMes.Api.Modules.Identity;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.Quality.Features.Hold;

public static class HoldEndpoint
{
    public static void MapHold(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/lots/{lotId}/hold", async (
            string lotId, HoldLotRequest request, HoldHandler handler, CancellationToken ct) =>
            (await handler.HandleAsync(lotId, request, ct)).ToHttpResult())
            .RequireAuthorization(Policies.Inspect);
}
