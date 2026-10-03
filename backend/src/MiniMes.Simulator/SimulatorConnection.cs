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
            .WithAutomaticReconnect(new RetryForeverPolicy())
            .Build();
}

/// <summary>Retries every 5 s without end, so an API outage of any length ends in a reconnect and a reload.</summary>
public sealed class RetryForeverPolicy : IRetryPolicy
{
    public static readonly TimeSpan Delay = TimeSpan.FromSeconds(5);

    public TimeSpan? NextRetryDelay(RetryContext retryContext) => Delay;
}
