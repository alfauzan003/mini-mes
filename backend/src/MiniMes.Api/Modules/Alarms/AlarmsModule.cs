namespace MiniMes.Api.Modules.Alarms;

public static class AlarmsModule
{
    public static IServiceCollection AddAlarmsModule(this IServiceCollection services)
    {
        services.AddScoped<AlarmService>();
        return services;
    }

    public static IEndpointRouteBuilder MapAlarmsEndpoints(this IEndpointRouteBuilder app) => app;
}
