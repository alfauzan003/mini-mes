using Microsoft.AspNetCore.SignalR;

namespace MiniMes.Api.Shared.Realtime;

/// <summary>Sends shop floor events to every client of <see cref="ShopfloorHub"/>; a failed send is logged and skipped.</summary>
public sealed class SignalRRealtimePublisher(
    IHubContext<ShopfloorHub> shopfloor, ILogger<SignalRRealtimePublisher> logger) : IRealtimePublisher
{
    public async Task PublishAsync(IReadOnlyList<RealtimeEvent> events, CancellationToken ct)
    {
        foreach (var e in events)
        {
            try
            {
                switch (e.Audience)
                {
                    case RealtimeAudience.Shopfloor:
                        await shopfloor.Clients.All.SendAsync(e.Method, e.Payload, ct);
                        break;
                    case RealtimeAudience.Simulators:
                        // The simulator hub does not exist yet, so there is no one to send these to.
                        break;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                logger.LogDebug("Publishing stopped: the request was cancelled");
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not publish {Method} to {Audience} clients", e.Method, e.Audience);
            }
        }
    }
}
