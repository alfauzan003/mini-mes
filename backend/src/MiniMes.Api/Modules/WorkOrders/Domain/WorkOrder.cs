using MiniMes.Api.Shared.Results;
using EquipmentEntity = MiniMes.Api.Modules.Equipment.Domain.Equipment;

namespace MiniMes.Api.Modules.WorkOrders.Domain;

/// <summary>The equipment that will run one route step of a work order.</summary>
public record OperationAssignmentInput(OperationCode Operation, EquipmentEntity Equipment);

public class WorkOrder
{
    private readonly List<WorkOrderOperation> _operations = [];

    private WorkOrder()
    {
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Number { get; private set; } = "";
    public Guid ProductId { get; private set; }
    public int TargetQty { get; private set; }
    public DateTimeOffset PlannedStart { get; private set; }
    public DateTimeOffset PlannedEnd { get; private set; }
    public WorkOrderStatus Status { get; private set; } = WorkOrderStatus.Planned;
    public WorkOrderStatus? StatusBeforeHold { get; private set; }
    public int GoodCount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public uint Version { get; private set; }

    /// <summary>One entry per route step, in route order.</summary>
    public IReadOnlyList<WorkOrderOperation> Operations => _operations;

    public static Result<WorkOrder> Create(
        string number, Product product, int targetQty, DateTimeOffset plannedStart, DateTimeOffset plannedEnd,
        IReadOnlyList<OperationAssignmentInput> assignments, DateTimeOffset now)
    {
        var error = Validate(targetQty, plannedStart, plannedEnd, assignments, product);
        if (error is not null)
        {
            return error;
        }

        var wo = new WorkOrder
        {
            Number = number,
            ProductId = product.Id,
            TargetQty = targetQty,
            PlannedStart = plannedStart,
            PlannedEnd = plannedEnd,
            CreatedAt = now
        };
        foreach (var step in product.Route)
        {
            var equipment = assignments.Single(a => a.Operation == step.Operation).Equipment;
            wo._operations.Add(new WorkOrderOperation(wo.Id, step.Operation, step.Seq, equipment.Id));
        }

        return wo;
    }

    public Result Update(
        int targetQty, DateTimeOffset plannedStart, DateTimeOffset plannedEnd,
        IReadOnlyList<OperationAssignmentInput> assignments, Product product)
    {
        if (Status != WorkOrderStatus.Planned)
        {
            return new Error(ErrorCodes.WoNotEditable, $"Work order {Number} is {Status} and can no longer be edited.");
        }

        var error = Validate(targetQty, plannedStart, plannedEnd, assignments, product);
        if (error is not null)
        {
            return error;
        }

        TargetQty = targetQty;
        PlannedStart = plannedStart;
        PlannedEnd = plannedEnd;
        foreach (var operation in _operations)
        {
            operation.AssignEquipment(assignments.Single(a => a.Operation == operation.Operation).Equipment.Id);
        }

        return Result.Success();
    }

    public Result Release()
    {
        if (Status != WorkOrderStatus.Planned)
        {
            return InvalidTransition(WorkOrderStatus.Released);
        }

        Status = WorkOrderStatus.Released;
        return Result.Success();
    }

    public Result Hold()
    {
        if (Status is not (WorkOrderStatus.Released or WorkOrderStatus.Running))
        {
            return InvalidTransition(WorkOrderStatus.Hold);
        }

        StatusBeforeHold = Status;
        Status = WorkOrderStatus.Hold;
        return Result.Success();
    }

    public Result Resume()
    {
        if (Status != WorkOrderStatus.Hold || StatusBeforeHold is null)
        {
            return new Error(
                ErrorCodes.WoInvalidTransition, $"Work order {Number} is {Status} and cannot be resumed.");
        }

        Status = StatusBeforeHold.Value;
        StatusBeforeHold = null;
        return Result.Success();
    }

    public Result Complete()
    {
        if (Status != WorkOrderStatus.Running)
        {
            return InvalidTransition(WorkOrderStatus.Completed);
        }

        Status = WorkOrderStatus.Completed;
        return Result.Success();
    }

    public Result EnsureCanTrackIn() =>
        Status is WorkOrderStatus.Released or WorkOrderStatus.Running
            ? Result.Success()
            : new Error(ErrorCodes.WoNotActive, $"Work order {Number} is {Status}; it does not accept track-in.");

    /// <summary>The first track-in starts a released order.</summary>
    public void OnTrackIn()
    {
        if (Status == WorkOrderStatus.Released)
        {
            Status = WorkOrderStatus.Running;
        }
    }

    /// <summary>Counts a good finished pancake; reaching the target closes a running or held order.</summary>
    public void RegisterFinishedPancake()
    {
        GoodCount++;
        if (GoodCount >= TargetQty && Status is WorkOrderStatus.Running or WorkOrderStatus.Hold)
        {
            Status = WorkOrderStatus.Completed;
            StatusBeforeHold = null;
        }
    }

    private static Error? Validate(
        int targetQty, DateTimeOffset plannedStart, DateTimeOffset plannedEnd,
        IReadOnlyList<OperationAssignmentInput> assignments, Product product)
    {
        if (targetQty <= 0)
        {
            return new Error(ErrorCodes.InvalidQuantity, "Target quantity must be greater than zero.");
        }

        if (plannedEnd <= plannedStart)
        {
            return new Error(ErrorCodes.InvalidDateRange, "Planned end must be after planned start.");
        }

        var route = product.Route;
        var coversRoute = assignments.Count == route.Count
            && route.All(step => assignments.Count(a => a.Operation == step.Operation) == 1);
        if (!coversRoute)
        {
            return new Error(
                ErrorCodes.WoInvalidAssignment,
                $"Assign exactly one equipment to each operation of product {product.Code}.");
        }

        var mismatch = assignments.FirstOrDefault(a => a.Equipment.Operation != a.Operation);
        return mismatch is null
            ? null
            : new Error(
                ErrorCodes.WoInvalidAssignment,
                $"Equipment {mismatch.Equipment.Code} runs {mismatch.Equipment.Operation}, not {mismatch.Operation}.");
    }

    private Error InvalidTransition(WorkOrderStatus target) =>
        new(ErrorCodes.WoInvalidTransition, $"Work order {Number} cannot go from {Status} to {target}.");
}
