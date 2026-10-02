using MiniMes.Api.Modules.Quality.Features.DefectCodes;
using MiniMes.Api.Modules.Quality.Features.Specs;
using MiniMes.Api.Shared.Quality;

namespace MiniMes.Api.Modules.Quality;

public static class QualityModule
{
    public static IServiceCollection AddQualityModule(this IServiceCollection services)
    {
        services.AddScoped<IInspectionRequirement, SpecInspectionRequirement>();
        services.AddScoped<UpdateSpecLimitsHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapQualityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapSpecEndpoints();
        app.MapDefectCodeEndpoints();
        return app;
    }
}
