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
}
