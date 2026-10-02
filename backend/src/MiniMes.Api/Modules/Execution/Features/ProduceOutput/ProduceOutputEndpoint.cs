using MiniMes.Api.Modules.Identity;
using MiniMes.Api.Shared.Http;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.Execution.Features.ProduceOutput;

public static class ProduceOutputEndpoint
{
    public static void MapProduceOutput(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/runs/{id:guid}/outputs", async (
            Guid id, ProduceOutputRequest request, ProduceOutputHandler handler, CancellationToken ct) =>
        {
            if (MalformedBody.HasNullItem(request.Outputs))
            {
                return MalformedBody.Problem("Every output line must be an object.");
            }

            return (await handler.HandleAsync(id, request, ct)).ToHttpResult();
        })
            .RequireAuthorization(Policies.Operate);
}
