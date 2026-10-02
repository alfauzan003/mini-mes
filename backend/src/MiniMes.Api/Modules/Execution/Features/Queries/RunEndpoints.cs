using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.Execution.Features.Queries;

public static class RunEndpoints
{
    public static void MapRunQueries(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/runs/{id:guid}", async (Guid id, RunQueries queries, CancellationToken ct) =>
        {
            var run = await queries.GetAsync(id, ct);
            Result<RunDto> result = run is not null
                ? run
                : new Error(ErrorCodes.RunNotFound, $"Run '{id}' was not found.", ErrorKind.NotFound);
            return result.ToHttpResult();
        }).RequireAuthorization();

        app.MapGet("/api/runs", async (string? equipment, bool? open, RunQueries queries, CancellationToken ct) =>
            TypedResults.Ok(await queries.ListAsync(equipment, open, ct))).RequireAuthorization();
    }
}
