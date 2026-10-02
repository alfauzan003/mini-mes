using MiniMes.Api.Modules.Lots.Features.Queries;

namespace MiniMes.Api.Modules.Lots;

public static class LotsModule
{
    public static IEndpointRouteBuilder MapLotsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapMaterials();
        return app;
    }
}
