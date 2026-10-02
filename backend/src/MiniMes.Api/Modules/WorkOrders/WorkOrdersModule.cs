using MiniMes.Api.Modules.WorkOrders.Features.ChangeStatus;
using MiniMes.Api.Modules.WorkOrders.Features.CreateWorkOrder;
using MiniMes.Api.Modules.WorkOrders.Features.Products;
using MiniMes.Api.Modules.WorkOrders.Features.Queries;
using MiniMes.Api.Modules.WorkOrders.Features.UpdateWorkOrder;

namespace MiniMes.Api.Modules.WorkOrders;

public static class WorkOrdersModule
{
    public static IServiceCollection AddWorkOrdersModule(this IServiceCollection services)
    {
        services.AddScoped<WorkOrderQueries>();
        services.AddScoped<CreateWorkOrderHandler>();
        services.AddScoped<UpdateWorkOrderHandler>();
        services.AddScoped<ChangeStatusHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapWorkOrdersEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapProducts();
        app.MapWorkOrderQueries();
        app.MapCreateWorkOrder();
        app.MapUpdateWorkOrder();
        app.MapChangeStatus();
        return app;
    }
}
