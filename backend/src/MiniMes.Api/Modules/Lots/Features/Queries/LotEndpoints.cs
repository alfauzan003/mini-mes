using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Http;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.Lots.Features.Queries;

public static class LotEndpoints
{
    public static void MapLotQueries(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/lots", async (
            string? status, string? type, string? nextOperation, string? workOrder, string? material, string? search,
            LotQueries queries, CancellationToken ct) =>
        {
            if (!EnumQuery.TryParseList<LotStatus>(status, out var statuses))
            {
                return EnumQuery.Invalid(nameof(status), status);
            }

            if (!EnumQuery.TryParse<LotType>(type, out var lotType))
            {
                return EnumQuery.Invalid(nameof(type), type);
            }

            if (!EnumQuery.TryParse<OperationCode>(nextOperation, out var next))
            {
                return EnumQuery.Invalid(nameof(nextOperation), nextOperation);
            }

            var filter = new LotFilter(statuses, lotType, next, workOrder, material, search);
            return TypedResults.Ok(await queries.ListAsync(filter, ct));
        }).RequireAuthorization();

        app.MapGet("/api/lots/{lotId}", async (string lotId, LotQueries queries, CancellationToken ct) =>
        {
            var lot = await queries.GetAsync(lotId, ct);
            Result<LotDto> result = lot is not null ? lot : LotNotFound(lotId);
            return result.ToHttpResult();
        }).RequireAuthorization();

        app.MapGet("/api/lots/{lotId}/events", async (string lotId, LotQueries queries, CancellationToken ct) =>
        {
            var events = await queries.EventsAsync(lotId, ct);
            // Result<T> cannot wrap an interface type, so the not-found case goes through the non-generic Result.
            return events is not null
                ? TypedResults.Ok(events)
                : ((Result)LotNotFound(lotId)).ToHttpResult();
        }).RequireAuthorization();
    }

    private static Error LotNotFound(string lotId) =>
        new(ErrorCodes.LotNotFound, $"Lot '{lotId}' was not found.", ErrorKind.NotFound);
}
