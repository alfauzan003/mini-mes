using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Results;

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

    public Result StartRun(Guid runId)
    {
        if (Status != EquipmentStatus.Idle || CurrentRunId is not null)
        {
            return new Error(ErrorCodes.EquipmentNotAvailable, $"Equipment {Code} is {Status} and cannot start a run.");
        }

        Status = EquipmentStatus.Running;
        CurrentRunId = runId;
        return Result.Success();
    }

    /// <summary>Clears the run; only a RUNNING machine returns to IDLE, so a DOWN machine stays down.</summary>
    public void EndRun()
    {
        CurrentRunId = null;
        if (Status == EquipmentStatus.Running)
        {
            Status = EquipmentStatus.Idle;
        }
    }
}
