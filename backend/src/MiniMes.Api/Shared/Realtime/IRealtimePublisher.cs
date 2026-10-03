namespace MiniMes.Api.Shared.Realtime;

/// <summary>Sends events to connected clients. Implementations log delivery failures instead of throwing.</summary>
public interface IRealtimePublisher
{
    Task PublishAsync(IReadOnlyList<RealtimeEvent> events, CancellationToken ct);
}
