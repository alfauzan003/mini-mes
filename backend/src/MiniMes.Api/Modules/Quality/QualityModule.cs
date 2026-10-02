using MiniMes.Api.Modules.Quality.Features.DefectCodes;
using MiniMes.Api.Modules.Quality.Features.Inspections;
using MiniMes.Api.Modules.Quality.Features.Queue;
using MiniMes.Api.Modules.Quality.Features.RecordInspection;
using MiniMes.Api.Modules.Quality.Features.Specs;
using MiniMes.Api.Shared.Quality;

namespace MiniMes.Api.Modules.Quality;

public static class QualityModule
{
    public static IServiceCollection AddQualityModule(this IServiceCollection services)
    {
        services.AddScoped<IInspectionRequirement, SpecInspectionRequirement>();
        services.AddScoped<UpdateSpecLimitsHandler>();
        services.AddScoped<InspectionQueries>();
        services.AddScoped<RecordInspectionHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapQualityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapSpecEndpoints();
        app.MapDefectCodeEndpoints();
        app.MapRecordInspection();
        app.MapInspectionQueries();
        app.MapInspectionQueue();
        return app;
    }
}
