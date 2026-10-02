using MiniMes.Api.Modules.Identity;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.Quality.Features.Dispositions;

public static class DispositionEndpoint
{
    public static void MapDisposition(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/lots/{lotId}/disposition", async (
            string lotId, DispositionRequest request, DispositionHandler handler, CancellationToken ct) =>
            (await handler.HandleAsync(lotId, request, ct)).ToHttpResult())
            .RequireAuthorization(Policies.Inspect);
}
