using MiniMes.Api.Modules.WorkOrders.Domain;

namespace MiniMes.Api.Modules.Equipment.Domain;

public class Equipment
{
    private Equipment()
    {
    }

    public Equipment(string code, string name, OperationCode operation, int? laneCount = null)
    {
        Code = code;
        Name = name;
        Operation = operation;
        LaneCount = laneCount;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public OperationCode Operation { get; private set; }
    public int? LaneCount { get; private set; }
    public EquipmentStatus Status { get; private set; } = EquipmentStatus.Idle;
    public EquipmentStatus? StatusBeforeDown { get; private set; }
    public Guid? CurrentRunId { get; private set; }
    public uint Version { get; private set; }
}
