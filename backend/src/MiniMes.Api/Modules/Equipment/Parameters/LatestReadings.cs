using MiniMes.Api.Modules.Equipment.Domain;

namespace MiniMes.Api.Modules.Equipment.Parameters;

public sealed record LiveReadingDto(
    string EquipmentCode,
    string Parameter,
    ParameterKind Kind,
    string Unit,
    decimal Value,
    decimal Low,
    decimal High,
    DateTimeOffset At);

/// <summary>The newest value of every equipment parameter, and when each was last stored. Safe for concurrent callers.</summary>
public sealed class LatestReadings
{
    private readonly object _gate = new();
    private readonly Dictionary<(string Equipment, string Parameter), LiveReadingDto> _latest = [];
    private readonly Dictionary<(string Equipment, string Parameter), DateTimeOffset> _persisted = [];

    public void Update(LiveReadingDto reading)
    {
        lock (_gate)
        {
            _latest[(reading.EquipmentCode, reading.Parameter)] = reading;
        }
    }

    /// <summary>All latest values, by equipment code and then definition order (kinds are declared in that order).</summary>
    public IReadOnlyList<LiveReadingDto> All()
    {
        lock (_gate)
        {
            return [.. _latest.Values
                .OrderBy(r => r.EquipmentCode, StringComparer.Ordinal)
                .ThenBy(r => r.Kind)
                .ThenBy(r => r.Parameter, StringComparer.Ordinal)];
        }
    }

    /// <summary>
    /// True, and records <paramref name="now"/>, when the parameter was never stored or the last store is at least
    /// <paramref name="interval"/> old. Atomic, so concurrent callers store one sample per interval.
    /// </summary>
    public bool TryMarkPersisted(string equipmentCode, string parameter, DateTimeOffset now, TimeSpan interval)
    {
        lock (_gate)
        {
            var key = (equipmentCode, parameter);
            if (_persisted.TryGetValue(key, out var last) && now - last < interval)
            {
                return false;
            }

            _persisted[key] = now;
            return true;
        }
    }
}
