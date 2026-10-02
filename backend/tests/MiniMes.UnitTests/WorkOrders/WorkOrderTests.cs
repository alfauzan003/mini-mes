using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Results;

namespace MiniMes.UnitTests.WorkOrders;

public class WorkOrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Start = Now.AddDays(1);
    private static readonly DateTimeOffset End = Now.AddDays(2);

    private static List<OperationAssignmentInput> ValidAssignments() =>
    [
        new(OperationCode.Mix, TestData.Equipment("MX01", OperationCode.Mix)),
        new(OperationCode.Coat, TestData.Equipment("CT01", OperationCode.Coat)),
        new(OperationCode.Cal, TestData.Equipment("CL01", OperationCode.Cal)),
        new(OperationCode.Slit, TestData.Equipment("SL01", OperationCode.Slit))
    ];

    private static WorkOrder NewPlanned(int targetQty = 10) =>
        WorkOrder.Create("WO-261002-001", TestData.CathodeProduct(), targetQty, Start, End, ValidAssignments(), Now)
            .Value;

    private static WorkOrder NewRunning(int targetQty = 10)
    {
        var wo = NewPlanned(targetQty);
        Assert.True(wo.Release().IsSuccess);
        wo.OnTrackIn();
        Assert.Equal(WorkOrderStatus.Running, wo.Status);
        return wo;
    }

    [Fact]
    public void Create_valid_is_planned_with_four_operations_in_route_order()
    {
        var product = TestData.CathodeProduct();
        var assignments = ValidAssignments();
        var expectedEquipment = assignments.Select(a => a.Equipment.Id).ToList();
        assignments.Reverse();

        var result = WorkOrder.Create("WO-261002-001", product, 10, Start, End, assignments, Now);

        Assert.True(result.IsSuccess);
        var wo = result.Value;
        Assert.Equal(WorkOrderStatus.Planned, wo.Status);
        Assert.Equal(product.Id, wo.ProductId);
        Assert.Equal(0, wo.GoodCount);
        Assert.Equal(Now, wo.CreatedAt);
        Assert.Equal(
            [OperationCode.Mix, OperationCode.Coat, OperationCode.Cal, OperationCode.Slit],
            wo.Operations.Select(o => o.Operation));
        Assert.Equal([10, 20, 30, 40], wo.Operations.Select(o => o.Seq));
        Assert.All(wo.Operations, o => Assert.Equal(wo.Id, o.WorkOrderId));
        Assert.Equal(expectedEquipment, wo.Operations.Select(o => o.EquipmentId));
    }

    [Fact]
    public void Create_with_zero_target_is_invalid_quantity()
    {
        var result = WorkOrder.Create("WO-1", TestData.CathodeProduct(), 0, Start, End, ValidAssignments(), Now);

        Assert.Equal(ErrorCodes.InvalidQuantity, result.Error!.Code);
    }

    [Fact]
    public void Create_with_end_before_start_is_invalid_date_range()
    {
        var result = WorkOrder.Create("WO-1", TestData.CathodeProduct(), 10, End, Start, ValidAssignments(), Now);

        Assert.Equal(ErrorCodes.InvalidDateRange, result.Error!.Code);
    }

    [Fact]
    public void Create_with_coater_assigned_to_mix_is_invalid_assignment()
    {
        var assignments = ValidAssignments();
        assignments[0] = new(OperationCode.Mix, TestData.Equipment("CT01", OperationCode.Coat));

        var result = WorkOrder.Create("WO-1", TestData.CathodeProduct(), 10, Start, End, assignments, Now);

        Assert.Equal(ErrorCodes.WoInvalidAssignment, result.Error!.Code);
    }

    [Fact]
    public void Create_missing_an_operation_is_invalid_assignment()
    {
        var assignments = ValidAssignments();
        assignments.RemoveAt(3);

        var result = WorkOrder.Create("WO-1", TestData.CathodeProduct(), 10, Start, End, assignments, Now);

        Assert.Equal(ErrorCodes.WoInvalidAssignment, result.Error!.Code);
    }

    [Fact]
    public void Create_with_an_operation_assigned_twice_is_invalid_assignment()
    {
        var assignments = ValidAssignments();
        assignments.Add(new(OperationCode.Slit, TestData.Equipment("SL02", OperationCode.Slit)));

        var result = WorkOrder.Create("WO-1", TestData.CathodeProduct(), 10, Start, End, assignments, Now);

        Assert.Equal(ErrorCodes.WoInvalidAssignment, result.Error!.Code);
    }

    [Fact]
    public void Update_while_planned_replaces_quantity_dates_and_equipment()
    {
        var wo = NewPlanned();
        var assignments = ValidAssignments();

        var result = wo.Update(25, Start.AddDays(1), End.AddDays(1), assignments, TestData.CathodeProduct());

        Assert.True(result.IsSuccess);
        Assert.Equal(25, wo.TargetQty);
        Assert.Equal(Start.AddDays(1), wo.PlannedStart);
        Assert.Equal(End.AddDays(1), wo.PlannedEnd);
        Assert.Equal(4, wo.Operations.Count);
        Assert.Equal(assignments.Select(a => a.Equipment.Id), wo.Operations.Select(o => o.EquipmentId));
    }

    [Fact]
    public void Update_with_invalid_input_changes_nothing()
    {
        var wo = NewPlanned();
        var before = wo.Operations.Select(o => o.EquipmentId).ToList();
        var assignments = ValidAssignments();
        assignments.RemoveAt(0);

        var result = wo.Update(25, Start, End, assignments, TestData.CathodeProduct());

        Assert.Equal(ErrorCodes.WoInvalidAssignment, result.Error!.Code);
        Assert.Equal(10, wo.TargetQty);
        Assert.Equal(before, wo.Operations.Select(o => o.EquipmentId));
    }

    [Fact]
    public void Update_after_release_is_not_editable()
    {
        var wo = NewPlanned();
        wo.Release();

        var result = wo.Update(25, Start, End, ValidAssignments(), TestData.CathodeProduct());

        Assert.Equal(ErrorCodes.WoNotEditable, result.Error!.Code);
        Assert.Equal(10, wo.TargetQty);
    }

    [Fact]
    public void Release_moves_planned_to_released_and_rejects_other_states()
    {
        var wo = NewPlanned();

        Assert.True(wo.Release().IsSuccess);
        Assert.Equal(WorkOrderStatus.Released, wo.Status);
        Assert.Equal(ErrorCodes.WoInvalidTransition, wo.Release().Error!.Code);
    }

    [Fact]
    public void Hold_then_resume_returns_to_previous_status()
    {
        var released = NewPlanned();
        released.Release();
        Assert.True(released.Hold().IsSuccess);
        Assert.Equal(WorkOrderStatus.Hold, released.Status);
        Assert.Equal(WorkOrderStatus.Released, released.StatusBeforeHold);
        Assert.True(released.Resume().IsSuccess);
        Assert.Equal(WorkOrderStatus.Released, released.Status);
        Assert.Null(released.StatusBeforeHold);

        var running = NewRunning();
        Assert.True(running.Hold().IsSuccess);
        Assert.Equal(WorkOrderStatus.Hold, running.Status);
        Assert.True(running.Resume().IsSuccess);
        Assert.Equal(WorkOrderStatus.Running, running.Status);
    }

    [Fact]
    public void Hold_from_planned_or_resume_when_not_on_hold_is_invalid_transition()
    {
        var wo = NewPlanned();

        Assert.Equal(ErrorCodes.WoInvalidTransition, wo.Hold().Error!.Code);
        Assert.Equal(ErrorCodes.WoInvalidTransition, wo.Resume().Error!.Code);
    }

    [Fact]
    public void Complete_from_planned_is_invalid_transition()
    {
        var wo = NewPlanned();

        var result = wo.Complete();

        Assert.Equal(ErrorCodes.WoInvalidTransition, result.Error!.Code);
        Assert.Equal(WorkOrderStatus.Planned, wo.Status);
    }

    [Fact]
    public void Complete_from_running_is_completed()
    {
        var wo = NewRunning();

        Assert.True(wo.Complete().IsSuccess);
        Assert.Equal(WorkOrderStatus.Completed, wo.Status);
    }

    [Fact]
    public void Track_in_allowed_only_when_released_or_running()
    {
        var wo = NewPlanned();
        Assert.Equal(ErrorCodes.WoNotActive, wo.EnsureCanTrackIn().Error!.Code);

        wo.Release();
        Assert.True(wo.EnsureCanTrackIn().IsSuccess);

        wo.OnTrackIn();
        Assert.True(wo.EnsureCanTrackIn().IsSuccess);

        wo.Hold();
        Assert.Equal(ErrorCodes.WoNotActive, wo.EnsureCanTrackIn().Error!.Code);

        wo.Resume();
        wo.Complete();
        Assert.Equal(ErrorCodes.WoNotActive, wo.EnsureCanTrackIn().Error!.Code);
    }

    [Fact]
    public void First_track_in_moves_released_to_running_and_later_ones_change_nothing()
    {
        var wo = NewPlanned();
        wo.Release();

        wo.OnTrackIn();
        Assert.Equal(WorkOrderStatus.Running, wo.Status);

        wo.OnTrackIn();
        Assert.Equal(WorkOrderStatus.Running, wo.Status);
    }

    [Fact]
    public void Reaching_target_completes_running_order()
    {
        var wo = NewRunning(targetQty: 2);

        wo.RegisterFinishedPancake();
        Assert.Equal(1, wo.GoodCount);
        Assert.Equal(WorkOrderStatus.Running, wo.Status);

        wo.RegisterFinishedPancake();
        Assert.Equal(2, wo.GoodCount);
        Assert.Equal(WorkOrderStatus.Completed, wo.Status);
    }

    [Fact]
    public void Reaching_target_while_on_hold_completes_order()
    {
        var wo = NewRunning(targetQty: 1);
        wo.Hold();

        wo.RegisterFinishedPancake();

        Assert.Equal(WorkOrderStatus.Completed, wo.Status);
        Assert.Null(wo.StatusBeforeHold);
    }
}
