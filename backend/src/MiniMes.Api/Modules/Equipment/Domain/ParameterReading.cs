namespace MiniMes.Api.Modules.Equipment.Domain;

/// <summary>One stored sample of a process value; the live stream is throttled before it reaches here.</summary>
public class ParameterReading
{
    private ParameterReading()
    {
    }

    public ParameterReading(Guid equipmentId, string parameter, decimal value, DateTimeOffset recordedAt)
    {
        EquipmentId = equipmentId;
        Parameter = parameter;
        Value = value;
        RecordedAt = recordedAt;
    }

    public long Id { get; private set; }
    public Guid EquipmentId { get; private set; }
    public string Parameter { get; private set; } = "";
    public decimal Value { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
}
