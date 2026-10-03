using MiniMes.Api.Modules.Equipment.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Results;

namespace MiniMes.UnitTests.EquipmentModule;

public class EquipmentTests
{
    [Fact]
    public void Start_run_on_idle_sets_running()
    {
        var coater = TestData.Equipment("COAT-01", OperationCode.Coat);
        var runId = Guid.NewGuid();

        var result = coater.StartRun(runId);

        Assert.True(result.IsSuccess);
        Assert.Equal(EquipmentStatus.Running, coater.Status);
        Assert.Equal(runId, coater.CurrentRunId);
    }

    [Fact]
    public void Start_run_twice_is_not_available()
    {
        var coater = TestData.Equipment("COAT-01", OperationCode.Coat);
        var firstRunId = Guid.NewGuid();
        coater.StartRun(firstRunId);

        var result = coater.StartRun(Guid.NewGuid());

        Assert.Equal(ErrorCodes.EquipmentNotAvailable, result.Error?.Code);
        Assert.Equal(firstRunId, coater.CurrentRunId);
    }

    [Fact]
    public void Start_run_in_maintenance_is_not_available()
    {
        var coater = TestData.Equipment("COAT-01", OperationCode.Coat, EquipmentStatus.Maintenance);

        var result = coater.StartRun(Guid.NewGuid());

        Assert.Equal(ErrorCodes.EquipmentNotAvailable, result.Error?.Code);
        Assert.Null(coater.CurrentRunId);
    }

    [Fact]
    public void End_run_returns_to_idle()
    {
        var coater = TestData.Equipment("COAT-01", OperationCode.Coat);
        coater.StartRun(Guid.NewGuid());

        coater.EndRun();

        Assert.Equal(EquipmentStatus.Idle, coater.Status);
        Assert.Null(coater.CurrentRunId);
    }

    [Fact]
    public void Start_maintenance_from_idle_records_change()
    {
        var coater = TestData.Equipment("COAT-01", OperationCode.Coat);

        var result = coater.StartMaintenance();

        Assert.True(result.IsSuccess);
        Assert.Equal(EquipmentStatus.Maintenance, coater.Status);
        var change = Assert.Single(coater.PendingStatusChanges);
        Assert.Equal(new EquipmentStatusChange(EquipmentStatus.Idle, EquipmentStatus.Maintenance, "Maintenance started"), change);
    }

    [Fact]
    public void Start_maintenance_while_running_is_not_available()
    {
        var coater = TestData.Equipment("COAT-01", OperationCode.Coat);
        coater.StartRun(Guid.NewGuid());

        var result = coater.StartMaintenance();

        Assert.Equal(ErrorCodes.EquipmentNotAvailable, result.Error?.Code);
        Assert.Equal(EquipmentStatus.Running, coater.Status);
    }

    [Fact]
    public void End_maintenance_when_idle_is_invalid_transition()
    {
        var coater = TestData.Equipment("COAT-01", OperationCode.Coat);

        var result = coater.EndMaintenance();

        Assert.Equal(ErrorCodes.EquipmentInvalidTransition, result.Error?.Code);
        Assert.Empty(coater.PendingStatusChanges);
    }

    [Fact]
    public void End_maintenance_returns_to_idle()
    {
        var coater = TestData.Equipment("COAT-01", OperationCode.Coat, EquipmentStatus.Maintenance);

        var result = coater.EndMaintenance();

        Assert.True(result.IsSuccess);
        Assert.Equal(EquipmentStatus.Idle, coater.Status);
        Assert.Equal("Maintenance ended", Assert.Single(coater.PendingStatusChanges).Reason);
    }

    [Fact]
    public void Go_down_from_running_keeps_run_and_recover_returns_running()
    {
        var coater = TestData.Equipment("COAT-01", OperationCode.Coat);
        var runId = Guid.NewGuid();
        coater.StartRun(runId);

        Assert.True(coater.GoDown("E-101"));

        Assert.Equal(EquipmentStatus.Down, coater.Status);
        Assert.Equal(EquipmentStatus.Running, coater.StatusBeforeDown);
        Assert.Equal(runId, coater.CurrentRunId);
        Assert.Equal("Alarm E-101", coater.PendingStatusChanges[^1].Reason);

        Assert.True(coater.RecoverFromDown("Alarm cleared"));

        Assert.Equal(EquipmentStatus.Running, coater.Status);
        Assert.Null(coater.StatusBeforeDown);
        Assert.Equal(new EquipmentStatusChange(EquipmentStatus.Down, EquipmentStatus.Running, "Alarm cleared"), coater.PendingStatusChanges[^1]);
    }

    [Fact]
    public void Recover_without_open_run_returns_idle()
    {
        var coater = TestData.Equipment("COAT-01", OperationCode.Coat);
        coater.GoDown("E-101");

        Assert.True(coater.RecoverFromDown("Alarm cleared"));

        Assert.Equal(EquipmentStatus.Idle, coater.Status);
    }

    [Fact]
    public void Recover_when_not_down_returns_false()
    {
        var coater = TestData.Equipment("COAT-01", OperationCode.Coat);

        Assert.False(coater.RecoverFromDown("x"));
        Assert.Empty(coater.PendingStatusChanges);
    }

    [Theory]
    [InlineData(EquipmentStatus.Maintenance)]
    [InlineData(EquipmentStatus.Down)]
    public void Go_down_in_maintenance_or_down_changes_nothing(EquipmentStatus status)
    {
        var coater = TestData.Equipment("COAT-01", OperationCode.Coat, status);

        Assert.False(coater.GoDown("E-101"));

        Assert.Equal(status, coater.Status);
        Assert.Empty(coater.PendingStatusChanges);
    }

    [Fact]
    public void End_run_while_down_stays_down()
    {
        var coater = TestData.Equipment("COAT-01", OperationCode.Coat);
        coater.StartRun(Guid.NewGuid());
        coater.GoDown("E-101");
        coater.ClearPendingStatusChanges();

        coater.EndRun();

        Assert.Equal(EquipmentStatus.Down, coater.Status);
        Assert.Null(coater.CurrentRunId);
        Assert.Empty(coater.PendingStatusChanges);
    }

    [Fact]
    public void Start_and_end_run_record_track_in_and_track_out()
    {
        var coater = TestData.Equipment("COAT-01", OperationCode.Coat);

        coater.StartRun(Guid.NewGuid());
        coater.EndRun();

        Assert.Equal(
            [
                new EquipmentStatusChange(EquipmentStatus.Idle, EquipmentStatus.Running, "Track-in"),
                new EquipmentStatusChange(EquipmentStatus.Running, EquipmentStatus.Idle, "Track-out")
            ],
            coater.PendingStatusChanges);
    }
}
