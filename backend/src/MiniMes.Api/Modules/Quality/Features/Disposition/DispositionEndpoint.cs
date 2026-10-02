using MiniMes.Api.Modules.Identity;
using MiniMes.Api.Modules.Quality.Domain;
using MiniMes.Api.Shared.Http;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.Quality.Features.Dispositions;

public static class DispositionEndpoint
{
    public static void MapDisposition(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/lots/{lotId}/disposition", async (
            string lotId, DispositionRequest request, DispositionHandler handler, CancellationToken ct) =>
        {
            // The enum binds from numbers too, and Release is 0: a missing or unknown decision must not default to an action.
            if (request.Decision is not { } decision || !Enum.IsDefined(decision))
            {
                return MalformedBody.Problem("Give a decision of RELEASE or SCRAP.");
            }

            return (await handler.HandleAsync(lotId, decision, request.Reason, ct)).ToHttpResult();
        })
            .RequireAuthorization(Policies.Inspect);
}
