using MiniMes.Api.Modules.WorkOrders.Features.Products;

namespace MiniMes.Api.Modules.WorkOrders;

public static class WorkOrdersModule
{
    public static IEndpointRouteBuilder MapWorkOrdersEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapProducts();
        return app;
    }
}
