using MiniMes.Api.Modules.Carriers.Domain;
using MiniMes.Api.Modules.Equipment.Domain;
using MiniMes.Api.Modules.Execution;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;

namespace MiniMes.IntegrationTests.Execution;

[Collection("api")]
public class FullFlowTests(MesApiFactory api) : IAsyncLifetime
{
    private ProductionDriver _driver = null!;

    public async ValueTask InitializeAsync()
    {
        await api.ResetDatabaseAsync();
        _driver = new ProductionDriver(api);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Full_cathode_flow_completes_work_order()
    {
        var flow = await _driver.RunFullFlowAsync(target: 8);
        var (wo, raws, foil, slurry, roll) = (flow.WorkOrder, flow.Raws, flow.Foil, flow.Slurry, flow.Electrode);

        var order = await _driver.WorkOrderAsync(wo);
        Assert.Equal(WorkOrderStatus.Completed, order.Status);
        Assert.Equal(8, order.GoodCount);
        Assert.All(order.Operations, o => Assert.Equal(1, o.RunCount));
        Assert.Equal(
            [480m, 1200m, 1180m, 1160m], order.Operations.OrderBy(o => o.Seq).Select(o => o.OutputQty));

        foreach (var code in new[] { "MX01", "CT01", "CP01", "SL01" })
        {
            var equipment = await _driver.EquipmentAsync(code);
            Assert.Equal(EquipmentStatus.Idle, equipment.Status);
            Assert.Null(equipment.OpenRun);
        }

        Assert.Equal(CarrierStatus.Empty, (await _driver.CarrierAsync("BB-0001")).Status);
        Assert.Equal(CarrierStatus.Empty, (await _driver.CarrierAsync("BB-0002")).Status);

        foreach (var raw in raws)
        {
            var lot = await _driver.LotAsync(raw);
            Assert.Equal(LotStatus.Wait, lot.Status);
            Assert.Equal(400m, lot.Qty);
        }

        var foilLot = await _driver.LotAsync(foil);
        Assert.Equal(LotStatus.Wait, foilLot.Status);
        Assert.Equal(4700m, foilLot.Qty);
        Assert.Equal(LotStatus.Consumed, (await _driver.LotAsync(slurry)).Status);
        var electrode = await _driver.LotAsync(roll);
        Assert.Equal(LotStatus.Consumed, electrode.Status);
        Assert.Null(electrode.CurrentCarrier);

        var next = await _driver.TrackInAsync("MX01", wo, OperationCode.Mix, (await _driver.MaterialLotsAsync("NCM811")));
        await ProductionDriver.AssertErrorAsync(next, 422, "WO_NOT_ACTIVE");
    }
}
