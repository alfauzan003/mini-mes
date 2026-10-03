using Microsoft.AspNetCore.SignalR;
using MiniMes.Api.Modules.Equipment.Machine;

namespace MiniMes.Api.Shared.Realtime;

/// <summary>Sends shop floor events to every client of <see cref="ShopfloorHub"/>; a failed send is logged and skipped.</summary>
public sealed class SignalRRealtimePublisher(
    IHubContext<ShopfloorHub> shopfloor, IHubContext<MachineHub> machines, ILogger<SignalRRealtimePublisher> logger) : IRealtimePublisher
{
    private Task SendToSimulatorsAsync(RealtimeEvent e, CancellationToken ct)
    {
        var group = machines.Clients.Group(MachineHub.SimulatorsGroup);
        // Simulators receive a status change as two arguments, not as the event object.
        return e.Payload is EquipmentStatusEvent status
            ? group.SendAsync(e.Method, status.Code, status.Status, ct)
            : group.SendAsync(e.Method, e.Payload, ct);
    }

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
                        await SendToSimulatorsAsync(e, ct);
                        break;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                logger.LogDebug("Publishing stopped: the caller cancelled it");
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not publish {Method} to {Audience} clients", e.Method, e.Audience);
            }
        }
    }
}
