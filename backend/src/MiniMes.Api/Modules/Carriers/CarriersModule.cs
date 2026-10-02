using MiniMes.Api.Modules.Carriers.Features.Queries;

namespace MiniMes.Api.Modules.Carriers;

public static class CarriersModule
{
    public static IEndpointRouteBuilder MapCarriersEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCarrierQueries();
        return app;
    }
}
