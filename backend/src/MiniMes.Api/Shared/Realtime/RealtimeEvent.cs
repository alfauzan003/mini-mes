namespace MiniMes.Api.Shared.Realtime;

/// <summary>Who receives an event: the shop floor screens, or the equipment simulators.</summary>
public enum RealtimeAudience
{
    Shopfloor,
    Simulators
}

/// <summary>A message for connected clients: the hub method they listen on and its payload.</summary>
public sealed record RealtimeEvent(RealtimeAudience Audience, string Method, object Payload);
