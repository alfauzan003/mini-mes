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

    /// <summary>A run can start only on an IDLE machine that has no open run.</summary>
    public Result EnsureCanStartRun() =>
        Status == EquipmentStatus.Idle && CurrentRunId is null
            ? Result.Success()
            : new Error(ErrorCodes.EquipmentNotAvailable, $"Equipment {Code} is {Status} and cannot start a run.");

    private readonly List<EquipmentStatusChange> _pendingStatusChanges = [];

    /// <summary>Status changes not yet written to the status log; not persisted with the entity.</summary>
    public IReadOnlyList<EquipmentStatusChange> PendingStatusChanges => _pendingStatusChanges;

    public void ClearPendingStatusChanges() => _pendingStatusChanges.Clear();

    public Result StartRun(Guid runId)
    {
        var available = EnsureCanStartRun();
        if (!available.IsSuccess)
        {
            return available;
        }

        ChangeStatus(EquipmentStatus.Running, "Track-in");
        CurrentRunId = runId;
        return Result.Success();
    }

    /// <summary>Clears the run; only a RUNNING machine returns to IDLE, so a DOWN machine stays down.</summary>
    public void EndRun()
    {
        CurrentRunId = null;
        if (Status == EquipmentStatus.Running)
        {
            ChangeStatus(EquipmentStatus.Idle, "Track-out");
        }
    }

    public Result StartMaintenance()
    {
        if (Status != EquipmentStatus.Idle || CurrentRunId is not null)
        {
            return new Error(
                ErrorCodes.EquipmentNotAvailable, $"Equipment {Code} is {Status} and cannot start maintenance.");
        }

        ChangeStatus(EquipmentStatus.Maintenance, "Maintenance started");
        return Result.Success();
    }

    public Result EndMaintenance()
    {
        if (Status != EquipmentStatus.Maintenance)
        {
            return new Error(
                ErrorCodes.EquipmentInvalidTransition, $"Equipment {Code} is {Status}, not in maintenance.");
        }

        ChangeStatus(EquipmentStatus.Idle, "Maintenance ended");
        return Result.Success();
    }

    /// <summary>An alarm takes an IDLE or RUNNING machine DOWN; the open run is kept. False when it did not apply.</summary>
    public bool GoDown(string alarmCode)
    {
        if (Status is not (EquipmentStatus.Idle or EquipmentStatus.Running))
        {
            return false;
        }

        StatusBeforeDown = Status;
        ChangeStatus(EquipmentStatus.Down, $"Alarm {alarmCode}");
        return true;
    }

    /// <summary>A DOWN machine resumes RUNNING if its run is still open, else IDLE. False when not DOWN.</summary>
    public bool RecoverFromDown(string reason)
    {
        if (Status != EquipmentStatus.Down)
        {
            return false;
        }

        ChangeStatus(CurrentRunId is null ? EquipmentStatus.Idle : EquipmentStatus.Running, reason);
        StatusBeforeDown = null;
        return true;
    }

    private void ChangeStatus(EquipmentStatus to, string reason)
    {
        _pendingStatusChanges.Add(new EquipmentStatusChange(Status, to, reason));
        Status = to;
    }
}
