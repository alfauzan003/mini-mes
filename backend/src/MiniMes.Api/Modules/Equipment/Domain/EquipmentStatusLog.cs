namespace MiniMes.Api.Modules.Equipment.Domain;

public class EquipmentStatusLog
{
    private EquipmentStatusLog()
    {
    }

    public EquipmentStatusLog(Guid equipmentId, EquipmentStatusChange change, DateTimeOffset changedAt)
    {
        EquipmentId = equipmentId;
        From = change.From;
        To = change.To;
        Reason = change.Reason;
        ChangedAt = changedAt;
    }

    public long Id { get; private set; }
    public Guid EquipmentId { get; private set; }
    public EquipmentStatus From { get; private set; }
    public EquipmentStatus To { get; private set; }
    public string Reason { get; private set; } = "";
    public DateTimeOffset ChangedAt { get; private set; }
}
