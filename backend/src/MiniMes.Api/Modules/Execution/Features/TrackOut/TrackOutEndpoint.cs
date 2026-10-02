using MiniMes.Api.Modules.Identity;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.Execution.Features.TrackOut;

/// <param name="Consumptions">
/// Quantity actually used per input lot. A lot that is not listed is used up entirely; null lists none.
/// </param>
public sealed record TrackOutRequest(IReadOnlyList<Consumption>? Consumptions);

/// <param name="LotId">String lot ID or scanned code of one of the run's input lots.</param>
public sealed record Consumption(string LotId, decimal ConsumedQty);

public static class TrackOutEndpoint
{
    public static void MapTrackOut(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/runs/{id:guid}/track-out", async (
            Guid id, TrackOutRequest request, TrackOutHandler handler, CancellationToken ct) =>
            (await handler.HandleAsync(id, request, ct)).ToHttpResult())
            .RequireAuthorization(Policies.Operate);
}
