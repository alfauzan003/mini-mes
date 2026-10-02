using MiniMes.Api.Modules.Carriers.Domain;
using MiniMes.Api.Modules.Equipment.Domain;
using MiniMes.Api.Modules.Execution;
using MiniMes.Api.Modules.Execution.Features.TrackOut;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;

namespace MiniMes.IntegrationTests.Execution;

[Collection("api")]
public class FullFlowTests(MesApiFactory api) : IAsyncLifetime
{
    private static readonly string[] CathodeMix = ["NCM811", "PVDF", "SUPER-P", "NMP"];

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
        // Written as a plain sequence of driver calls so it can move into ProductionDriver.RunFullFlowAsync.
        var wo = await _driver.CreateReleasedWorkOrderAsync(target: 8);
        var raws = await _driver.MaterialLotsAsync(CathodeMix);
        var foil = (await _driver.MaterialLotsAsync("AL-FOIL")).Single();

        // MIX: 4 RAW lots become one slurry lot; 100 kg of each RAW lot is used.
        var mix = await _driver.TrackInOkAsync("MX01", wo, OperationCode.Mix, raws);
        var slurry = (await _driver.ProduceOkAsync(mix.Id, new OutputLine(null, null, 480m, 20m)))
            .Outputs.Single().LotId!;
        await _driver.TrackOutOkAsync(mix.Id, [.. raws.Select(raw => new Consumption(raw, 100m))]);

        // COAT: foil and slurry become one roll on BB-0001; 1300 m of foil and all the slurry are used.
        var coat = await _driver.TrackInOkAsync("CT01", wo, OperationCode.Coat, foil, slurry);
        var roll = (await _driver.ProduceOkAsync(coat.Id, new OutputLine("BB-0001", null, 1200m, 20m)))
            .Outputs.Single().LotId!;
        await _driver.TrackOutOkAsync(coat.Id, new Consumption(foil, 1300m));

        // CAL: the same roll moves from BB-0001 to BB-0002.
        var cal = await _driver.TrackInOkAsync("CP01", wo, OperationCode.Cal, "BB-0001");
        await _driver.ProduceOkAsync(cal.Id, new OutputLine("BB-0002", null, 1180m, 20m));
        await _driver.TrackOutOkAsync(cal.Id);

        // SLIT: 8 lanes of 145 m on pancake cores PC-0001..8.
        var slit = await _driver.TrackInOkAsync("SL01", wo, OperationCode.Slit, "BB-0002");
        await _driver.ProduceOkAsync(
            slit.Id, [.. Enumerable.Range(1, 8).Select(lane => new OutputLine($"PC-{lane:0000}", lane, 145m, 2m))]);
        await _driver.TrackOutOkAsync(slit.Id);

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
