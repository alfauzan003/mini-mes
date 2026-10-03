using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;

namespace MiniMes.Simulator;

public static class SimulatorConnection
{
    public const string KeyHeader = "X-Machine-Key";

    public static HubConnection Build(SimulatorOptions options, Action<HttpConnectionOptions>? configure = null) =>
        new HubConnectionBuilder()
            .WithUrl(options.HubUrl, o =>
            {
                o.Headers[KeyHeader] = options.ApiKey;
                configure?.Invoke(o);
            })
            .AddJsonProtocol(p => p.PayloadSerializerOptions.Converters.Add(
                new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper)))
            .WithAutomaticReconnect()
            .Build();
}
