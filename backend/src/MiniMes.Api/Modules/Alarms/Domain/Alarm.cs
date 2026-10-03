using MiniMes.Api.Shared.Results;
using AlarmCodeEntity = MiniMes.Api.Modules.Alarms.Domain.AlarmCode;
using Outcome = MiniMes.Api.Shared.Results.Result;

namespace MiniMes.Api.Modules.Alarms.Domain;

public class Alarm
{
    private Alarm()
    {
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid EquipmentId { get; private set; }
    public string AlarmCode { get; private set; } = "";
    public AlarmSeverity Severity { get; private set; }
    public DateTimeOffset RaisedAt { get; private set; }
    public DateTimeOffset? ClearedAt { get; private set; }
    public Guid? AcknowledgedById { get; private set; }
    public DateTimeOffset? AcknowledgedAt { get; private set; }
    public uint Version { get; private set; }

    public bool IsActive => ClearedAt is null;

    public static Alarm Raise(Guid equipmentId, AlarmCodeEntity code, DateTimeOffset now) =>
        new()
        {
            EquipmentId = equipmentId,
            AlarmCode = code.Code,
            Severity = code.Severity,
            RaisedAt = now
        };

    public Result Clear(DateTimeOffset now)
    {
        if (!IsActive)
        {
            return new Error(ErrorCodes.AlarmNotActive, $"Alarm {AlarmCode} is already cleared.");
        }

        ClearedAt = now;
        return Outcome.Success();
    }

    /// <summary>An alarm can be acknowledged once, whether it is still active or already cleared.</summary>
    public Result Acknowledge(Guid userId, DateTimeOffset now)
    {
        if (AcknowledgedAt is not null)
        {
            return new Error(ErrorCodes.AlarmAlreadyAcknowledged, $"Alarm {AlarmCode} is already acknowledged.");
        }

        AcknowledgedById = userId;
        AcknowledgedAt = now;
        return Outcome.Success();
    }
}
