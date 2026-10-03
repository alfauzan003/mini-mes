using MiniMes.Api.Shared.Http;

namespace MiniMes.Api.Shared.Realtime;

public static class RealtimeModule
{
    public static IServiceCollection AddRealtimeModule(this IServiceCollection services)
    {
        services.AddSignalR().AddJsonProtocol(o => o.PayloadSerializerOptions.AddMesConverters());
        services.AddSingleton<IRealtimePublisher, SignalRRealtimePublisher>();
        services.AddScoped<ChangeFeed>();
        return services;
    }
}
