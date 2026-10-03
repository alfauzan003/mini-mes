using MiniMes.Api.Modules.Alarms.Domain;

namespace MiniMes.Api.Modules.Alarms.Features.Queries;

/// <param name="DurationSeconds">Seconds from raise to clear; null while the alarm is still active.</param>
public sealed record AlarmDto(
    Guid Id,
    string EquipmentCode,
    string Code,
    string Message,
    AlarmSeverity Severity,
    DateTimeOffset RaisedAt,
    DateTimeOffset? ClearedAt,
    double? DurationSeconds,
    string? AcknowledgedBy,
    DateTimeOffset? AcknowledgedAt);
