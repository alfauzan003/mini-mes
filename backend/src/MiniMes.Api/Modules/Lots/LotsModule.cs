using Microsoft.Extensions.DependencyInjection.Extensions;
using MiniMes.Api.Modules.Lots.Features.Queries;
using MiniMes.Api.Modules.Lots.Features.RegisterMaterial;
using MiniMes.Api.Modules.Lots.LotIds;
using MiniMes.Api.Shared.Time;

namespace MiniMes.Api.Modules.Lots;

public static class LotsModule
{
    public static IServiceCollection AddLotsModule(this IServiceCollection services)
    {
        services.AddOptions<PlantOptions>()
            .BindConfiguration(PlantOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<PlantCalendar>();
        services.AddScoped<LotIdGenerator>();
        services.AddScoped<LotQueries>();
        services.AddScoped<RegisterMaterialHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapLotsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapMaterials();
        app.MapRegisterMaterial();
        app.MapLotQueries();
        return app;
    }
}
