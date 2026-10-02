using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Http;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.WorkOrders.Features.Queries;

public static class WorkOrderEndpoints
{
    public static void MapWorkOrderQueries(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/work-orders", async (string? status, WorkOrderQueries queries, CancellationToken ct) =>
        {
            if (!EnumQuery.TryParseList<WorkOrderStatus>(status, out var statuses))
            {
                return EnumQuery.Invalid(nameof(status), status);
            }

            return TypedResults.Ok(await queries.ListAsync(statuses, ct));
        }).RequireAuthorization();

        app.MapGet("/api/work-orders/{id:guid}", async (Guid id, WorkOrderQueries queries, CancellationToken ct) =>
        {
            var dto = await queries.GetAsync(id, ct);
            Result<WorkOrderDto> result = dto is not null ? dto : NotFound(id);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }

    public static Error NotFound(Guid id) =>
        new(ErrorCodes.WoNotFound, $"Work order '{id}' was not found.", ErrorKind.NotFound);
}
