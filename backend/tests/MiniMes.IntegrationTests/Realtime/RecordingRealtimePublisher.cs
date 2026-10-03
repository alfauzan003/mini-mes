using MiniMes.Api.Shared.Realtime;

namespace MiniMes.IntegrationTests.Realtime;

/// <summary>Keeps every published event in memory instead of sending it, so tests can assert on them.</summary>
public sealed class RecordingRealtimePublisher : IRealtimePublisher
{
    private readonly object _gate = new();
    private readonly List<RealtimeEvent> _events = [];

    public IReadOnlyList<RealtimeEvent> Events
    {
        get
        {
            lock (_gate)
            {
                return [.. _events];
            }
        }
    }

    public IEnumerable<T> Payloads<T>(string method) =>
        Events.Where(e => e.Method == method).Select(e => e.Payload).OfType<T>();

    public void Clear()
    {
        lock (_gate)
        {
            _events.Clear();
        }
    }

    /// <summary>Honours the token like a real transport would, so a cancelled publish records nothing.</summary>
    public Task PublishAsync(IReadOnlyList<RealtimeEvent> events, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _events.AddRange(events);
        }

        return Task.CompletedTask;
    }
}
