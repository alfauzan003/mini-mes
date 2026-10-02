using MiniMes.Api.Modules.Execution.Domain;
using MiniMes.Api.Shared.Results;

namespace MiniMes.UnitTests.Execution;

public class ProductionRunTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 8, 0, 0, TimeSpan.Zero);

    private static ProductionRun StartedRun(Guid? primaryLotId = null) => ProductionRun.Start(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        [(primaryLotId ?? Guid.NewGuid(), RunInputRole.Primary)], Now);

    [Fact]
    public void Start_opens_run_with_inputs()
    {
        var foilId = Guid.NewGuid();
        var slurryId = Guid.NewGuid();

        var run = ProductionRun.Start(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            [(foilId, RunInputRole.Primary), (slurryId, RunInputRole.Secondary)], Now);

        Assert.True(run.IsOpen);
        Assert.Equal(Now, run.StartedAt);
        Assert.Equal(2, run.Inputs.Count);
        Assert.Equal(foilId, run.PrimaryLotId);
    }

    [Fact]
    public void End_sums_output_quantities()
    {
        var primaryLotId = Guid.NewGuid();
        var run = StartedRun(primaryLotId);
        run.AddOutput(Guid.NewGuid(), Guid.NewGuid(), 1, 900, 100);
        run.AddOutput(Guid.NewGuid(), Guid.NewGuid(), 2, 950, 50);

        var result = run.End(new Dictionary<Guid, decimal> { [primaryLotId] = 2000 }, Now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.False(run.IsOpen);
        Assert.Equal(Now.AddHours(1), run.EndedAt);
        Assert.Equal(1850, run.GoodQty);
        Assert.Equal(150, run.RejectQty);
        Assert.Equal(2000, run.Inputs.Single().ConsumedQty);
    }

    [Fact]
    public void Add_output_after_end_is_run_not_open()
    {
        var run = StartedRun();
        run.End(new Dictionary<Guid, decimal>(), Now.AddHours(1));

        var result = run.AddOutput(Guid.NewGuid(), null, null, 10, 0);

        Assert.Equal(ErrorCodes.RunNotOpen, result.Error?.Code);
        Assert.Empty(run.Outputs);
    }

    [Fact]
    public void End_twice_is_run_not_open()
    {
        var run = StartedRun();
        run.End(new Dictionary<Guid, decimal>(), Now.AddHours(1));

        var result = run.End(new Dictionary<Guid, decimal>(), Now.AddHours(2));

        Assert.Equal(ErrorCodes.RunNotOpen, result.Error?.Code);
    }

    [Fact]
    public void Negative_reject_is_invalid_quantity()
    {
        var run = StartedRun();

        var result = run.AddOutput(Guid.NewGuid(), null, null, 100, -5);

        Assert.Equal(ErrorCodes.InvalidQuantity, result.Error?.Code);
        Assert.Empty(run.Outputs);
    }

    [Fact]
    public void Negative_good_is_invalid_quantity()
    {
        var run = StartedRun();

        var result = run.AddOutput(Guid.NewGuid(), null, null, -1, 0);

        Assert.Equal(ErrorCodes.InvalidQuantity, result.Error?.Code);
    }
}
