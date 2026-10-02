using MiniMes.Api.Modules.Identity;
using MiniMes.Api.Shared.Http;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.Execution.Features.TrackOut;

/// <param name="Consumptions">
/// Quantity actually used per input lot. A lot that is not listed is used up entirely; null lists none.
/// </param>
public sealed record TrackOutRequest(IReadOnlyList<Consumption>? Consumptions);

/// <param name="LotId">String lot ID or scanned code of one of the run's input lots.</param>
/// <param name="ConsumedQty">Required: an omitted quantity is a malformed request, not zero.</param>
public sealed record Consumption(string LotId, decimal? ConsumedQty);

public static class TrackOutEndpoint
{
    public static void MapTrackOut(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/runs/{id:guid}/track-out", async (
            Guid id, TrackOutRequest request, TrackOutHandler handler, CancellationToken ct) =>
        {
            if (MalformedBody.HasNullItem(request.Consumptions)
                || request.Consumptions?.Any(c => c.ConsumedQty is null) == true)
            {
                return MalformedBody.Problem("Every consumption needs a lotId and a consumedQty.");
            }

            return (await handler.HandleAsync(id, request, ct)).ToHttpResult();
        })
            .RequireAuthorization(Policies.Operate);
}
