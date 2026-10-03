using MiniMes.Api.Modules.Alarms.Features.Acknowledge;
using MiniMes.Api.Modules.Alarms.Features.Queries;

namespace MiniMes.Api.Modules.Alarms;

public static class AlarmsModule
{
    public static IServiceCollection AddAlarmsModule(this IServiceCollection services)
    {
        services.AddScoped<AlarmService>();
        services.AddScoped<AlarmQueries>();
        services.AddScoped<AcknowledgeHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapAlarmsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapAlarmQueries();
        app.MapAcknowledge();
        return app;
    }
}
