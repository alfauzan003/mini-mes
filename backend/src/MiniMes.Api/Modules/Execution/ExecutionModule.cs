using MiniMes.Api.Modules.Execution.Features.ProduceOutput;
using MiniMes.Api.Modules.Execution.Features.Queries;
using MiniMes.Api.Modules.Execution.Features.TrackIn;
using MiniMes.Api.Modules.Execution.Features.TrackOut;

namespace MiniMes.Api.Modules.Execution;

public static class ExecutionModule
{
    public static IServiceCollection AddExecutionModule(this IServiceCollection services)
    {
        services.AddScoped<ScanResolver>();
        services.AddScoped<RunQueries>();
        services.AddScoped<TrackInHandler>();
        services.AddScoped<ProduceOutputHandler>();
        services.AddScoped<TrackOutHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapExecutionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapRunQueries();
        app.MapTrackIn();
        app.MapProduceOutput();
        app.MapTrackOut();
        return app;
    }
}
